using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ConferenceHallBooking.LoadTesting;

/// <summary>
/// Мікс GET / POST / PUT до різних endpoint'ів API (dev).
/// Більшість запитів — легкі GET; POST/PUT рідше.
/// PUT лише по залах, створених цим прогоном (seed не чіпаємо).
/// </summary>
internal sealed class ApiWorkload
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<Guid> SeedHallIds =
    [
        Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"),
        Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"),
        Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC")
    ];

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<Guid, byte> _createdHallIds = new();

    private ApiWorkload(HttpClient http) => _http = http;

    public int CreatedHallCount => _createdHallIds.Count;

    public static async Task<ApiWorkload> CreateAsync(HttpClient http, CancellationToken cancellationToken)
    {
        using var health = await http.GetAsync("/health", cancellationToken);
        health.EnsureSuccessStatusCode();

        using var hallsResponse = await http.GetAsync("/api/v1/halls", cancellationToken);
        hallsResponse.EnsureSuccessStatusCode();

        var halls = await hallsResponse.Content.ReadFromJsonAsync<List<HallDto>>(JsonOptions, cancellationToken)
                    ?? [];

        if (halls.Count == 0)
            throw new InvalidOperationException("API не повернув жодного залу. Переконайтесь, що seed виконано.");

        Console.WriteLine($"Warmup OK: /health, /api/v1/halls ({halls.Count} залів).");
        return new ApiWorkload(http);
    }

    public Task RunOneAsync(int index, LoadStatistics stats, CancellationToken cancellationToken)
    {
        // На 20 запитів: 40% GET halls, 25% available, 20% summary, 10% POST, 5% PUT.
        var slot = index % 20;
        return slot switch
        {
            < 8 => TimedRequest.ExecuteAsync(
                ct => _http.GetAsync("/api/v1/halls", ct), stats, cancellationToken),
            < 13 => TimedRequest.ExecuteAsync(
                GetAvailableAsync, stats, cancellationToken),
            < 17 => TimedRequest.ExecuteAsync(
                ct => _http.GetAsync("/api/v1/reports/summary", ct), stats, cancellationToken),
            < 19 => TimedRequest.ExecuteAsync(
                ct => PostHallAsync(index, ct), stats, cancellationToken, TrackCreatedHallAsync),
            _ => TimedRequest.ExecuteAsync(
                ct => PutOrFallbackAsync(index, ct), stats, cancellationToken)
        };
    }

    /// <summary>
    /// Soft-delete залів, створених цим прогоном, плюс залишків LoadDev-/LoadPut- (крім seed).
    /// </summary>
    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var toDelete = new HashSet<Guid>(_createdHallIds.Keys);

        try
        {
            using var hallsResponse = await _http.GetAsync("/api/v1/halls", cancellationToken);
            if (hallsResponse.IsSuccessStatusCode)
            {
                var halls = await hallsResponse.Content.ReadFromJsonAsync<List<HallDto>>(JsonOptions, cancellationToken)
                            ?? [];
                foreach (var hall in halls)
                {
                    if (SeedHallIds.Contains(hall.Id))
                        continue;
                    if (hall.Name.StartsWith("LoadDev-", StringComparison.Ordinal)
                        || hall.Name.StartsWith("LoadPut-", StringComparison.Ordinal))
                        toDelete.Add(hall.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cleanup: не вдалося отримати список залів ({ex.GetBaseException().Message}).");
        }

        if (toDelete.Count == 0)
        {
            Console.WriteLine("Cleanup: немає тестових залів для видалення.");
            return;
        }

        Console.WriteLine($"Cleanup: видалення {toDelete.Count} тестових залів (soft-delete)...");

        var ok = 0;
        var fail = 0;
        var sampleErrors = new ConcurrentBag<(Guid Id, string Reason)>();
        using var gate = new SemaphoreSlim(5, 5);

        var tasks = toDelete.Select(async id =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                using var response = await _http.DeleteAsync($"/api/v1/halls/{id}", cancellationToken);
                await response.Content.CopyToAsync(Stream.Null, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref ok);
                }
                else
                {
                    Interlocked.Increment(ref fail);
                    if (sampleErrors.Count < 5)
                        sampleErrors.Add((id, $"HTTP {(int)response.StatusCode}"));
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref fail);
                if (sampleErrors.Count < 5)
                    sampleErrors.Add((id, ex.GetBaseException().Message));
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        Console.WriteLine($"Cleanup: готово (OK={ok}, fail={fail}).");
        foreach (var (id, reason) in sampleErrors)
            Console.WriteLine($"  sample fail {id}: {reason}");

        if (fail > 0)
            Console.WriteLine("  Якщо багато fail — перезапустіть API і/або виконайте cleanup-test-halls.sql");
    }

    private async Task TrackCreatedHallAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await response.Content.CopyToAsync(Stream.Null, cancellationToken);
            return;
        }

        try
        {
            var hall = await response.Content.ReadFromJsonAsync<HallDto>(JsonOptions, cancellationToken);
            if (hall is not null && hall.Id != Guid.Empty)
                _createdHallIds[hall.Id] = 0;
        }
        catch
        {
            // Тіло вже спожито / невалідне — ігноруємо трекінг для цього запиту.
        }
    }

    private Task<HttpResponseMessage> GetAvailableAsync(CancellationToken cancellationToken)
    {
        var dayOffset = 365 + Random.Shared.Next(0, 30);
        var start = DateTime.UtcNow.Date.AddDays(dayOffset).AddHours(10);
        var end = start.AddHours(2);
        var capacity = 10 + (Random.Shared.Next(0, 5) * 10);
        var url =
            $"/api/v1/halls/available?start={Uri.EscapeDataString(start.ToString("o"))}" +
            $"&end={Uri.EscapeDataString(end.ToString("o"))}" +
            $"&requiredCapacity={capacity}";
        return _http.GetAsync(url, cancellationToken);
    }

    private Task<HttpResponseMessage> PostHallAsync(int index, CancellationToken cancellationToken)
    {
        var name = $"LoadDev-{index}-{Guid.NewGuid():N}"[..40];
        var json = $$"""
            {
              "name": "{{name}}",
              "capacity": {{30 + index % 70}},
              "baseHourlyRate": {{1500 + index % 20 * 100}},
              "services": [
                { "name": "Wi-Fi", "price": 300 },
                { "name": "Проєктор", "price": 500 }
              ]
            }
            """;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return _http.PostAsync("/api/v1/halls", content, cancellationToken);
    }

    private Task<HttpResponseMessage> PutOrFallbackAsync(int index, CancellationToken cancellationToken)
    {
        var created = _createdHallIds.Keys.ToArray();
        if (created.Length == 0)
        {
            // Ще немає створених залів — не чіпаємо seed PUT'ом.
            return _http.GetAsync("/api/v1/reports/summary", cancellationToken);
        }

        var hallId = created[index % created.Length];
        var json = $$"""
            {
              "name": "LoadPut-{{hallId.ToString("N")[..8]}}-{{index % 100}}",
              "capacity": {{40 + index % 60}},
              "baseHourlyRate": {{1600 + index % 15 * 100}},
              "services": [
                { "name": "Звук", "price": 700 }
              ]
            }
            """;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return _http.PutAsync($"/api/v1/halls/{hallId}", content, cancellationToken);
    }

    private sealed record HallDto(Guid Id, string Name);
}
