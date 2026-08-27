using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ConferenceHallBooking.LoadTesting;

/// <summary>
/// Готує мікс GET / POST / PUT запитів до різних endpoint'ів API (dev).
/// </summary>
internal sealed class ApiWorkload
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IReadOnlyList<Guid> _hallIds;

    private ApiWorkload(HttpClient http, IReadOnlyList<Guid> hallIds)
    {
        _http = http;
        _hallIds = hallIds;
    }

    public static async Task<ApiWorkload> CreateAsync(HttpClient http, CancellationToken cancellationToken)
    {
        using var health = await http.GetAsync("/health", cancellationToken);
        health.EnsureSuccessStatusCode();

        using var hallsResponse = await http.GetAsync("/api/v1/halls", cancellationToken);
        hallsResponse.EnsureSuccessStatusCode();

        var halls = await hallsResponse.Content.ReadFromJsonAsync<List<HallIdDto>>(JsonOptions, cancellationToken)
                    ?? [];

        if (halls.Count == 0)
            throw new InvalidOperationException("API не повернув жодного залу. Переконайтесь, що seed виконано.");

        Console.WriteLine($"Warmup OK: /health, /api/v1/halls ({halls.Count} залів).");
        return new ApiWorkload(http, halls.Select(h => h.Id).ToList());
    }

    public Task RunOneAsync(int index, LoadStatistics stats, CancellationToken cancellationToken)
        => TimedRequest.ExecuteAsync(ct => SendByIndexAsync(index, ct), stats, cancellationToken);

    private Task<HttpResponseMessage> SendByIndexAsync(int index, CancellationToken cancellationToken)
    {
        // Рівномірний мікс методів і endpoint'ів.
        return (index % 5) switch
        {
            0 => _http.GetAsync("/api/v1/halls", cancellationToken),
            1 => GetAvailableAsync(cancellationToken),
            2 => _http.GetAsync("/api/v1/reports/summary", cancellationToken),
            3 => PostHallAsync(index, cancellationToken),
            _ => PutHallAsync(index, cancellationToken)
        };
    }

    private Task<HttpResponseMessage> GetAvailableAsync(CancellationToken cancellationToken)
    {
        // Далекі дати — менше конфліктів із реальними бронюваннями в shared DB.
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

    private Task<HttpResponseMessage> PutHallAsync(int index, CancellationToken cancellationToken)
    {
        var hallId = _hallIds[index % _hallIds.Count];
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

    private sealed record HallIdDto(Guid Id);
}
