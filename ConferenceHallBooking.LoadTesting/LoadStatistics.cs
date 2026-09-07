using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;

namespace ConferenceHallBooking.LoadTesting;

internal sealed class LoadStatistics
{
    private readonly ConcurrentBag<long> _latenciesMs = [];
    private readonly ConcurrentDictionary<string, long> _errorReasons = new(StringComparer.Ordinal);
    private long _success;
    private long _errors;

    public void RecordSuccess(long elapsedMs)
    {
        Interlocked.Increment(ref _success);
        _latenciesMs.Add(elapsedMs);
    }

    public void RecordError(long elapsedMs, string reason)
    {
        Interlocked.Increment(ref _errors);
        _latenciesMs.Add(elapsedMs);
        _errorReasons.AddOrUpdate(reason, 1, static (_, n) => n + 1);
    }

    public LoadResult Snapshot(int taskCount, int concurrency, TimeSpan totalElapsed)
    {
        var latencies = _latenciesMs.ToArray();
        double avg = latencies.Length == 0 ? 0 : latencies.Average();
        long min = latencies.Length == 0 ? 0 : latencies.Min();
        long max = latencies.Length == 0 ? 0 : latencies.Max();

        var topErrors = _errorReasons
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => (kv.Key, kv.Value))
            .ToArray();

        return new LoadResult(
            TaskCount: taskCount,
            Concurrency: concurrency,
            TotalElapsed: totalElapsed,
            AverageMs: avg,
            MinMs: min,
            MaxMs: max,
            SuccessCount: Interlocked.Read(ref _success),
            ErrorCount: Interlocked.Read(ref _errors),
            TopErrors: topErrors);
    }
}

internal sealed record LoadResult(
    int TaskCount,
    int Concurrency,
    TimeSpan TotalElapsed,
    double AverageMs,
    long MinMs,
    long MaxMs,
    long SuccessCount,
    long ErrorCount,
    IReadOnlyList<(string Reason, long Count)> TopErrors)
{
    public void Print()
    {
        Console.WriteLine();
        Console.WriteLine($"=== Результат: tasks={TaskCount}, concurrency={Concurrency} ===");
        Console.WriteLine($"Загальний час:           {TotalElapsed.TotalSeconds:F2} с ({TotalElapsed.TotalMilliseconds:F0} мс)");
        Console.WriteLine($"Середній час відповіді:  {AverageMs:F2} мс");
        Console.WriteLine($"Мін. час відповіді:      {MinMs} мс");
        Console.WriteLine($"Макс. час відповіді:     {MaxMs} мс");
        Console.WriteLine($"Успішних запитів:        {SuccessCount}");
        Console.WriteLine($"Запитів з помилками:     {ErrorCount}");

        if (TopErrors.Count > 0)
        {
            Console.WriteLine("Топ помилок:");
            foreach (var (reason, count) in TopErrors)
                Console.WriteLine($"  [{count}] {reason}");
        }
    }
}

internal static class LoadResultTable
{
    public static void PrintComparison(IReadOnlyList<LoadResult> results)
    {
        if (results.Count <= 1)
            return;

        Console.WriteLine();
        Console.WriteLine("=== Порівняння сценаріїв ===");
        Console.WriteLine(
            $"{"Conc",6} {"Total(s)",10} {"Avg(ms)",10} {"Min",8} {"Max",8} {"OK",8} {"Err",8}");
        Console.WriteLine(new string('-', 64));

        foreach (var r in results)
        {
            Console.WriteLine(
                $"{r.Concurrency,6} {r.TotalElapsed.TotalSeconds,10:F2} {r.AverageMs,10:F2} {r.MinMs,8} {r.MaxMs,8} {r.SuccessCount,8} {r.ErrorCount,8}");
        }
    }
}

internal static class TimedRequest
{
    public static async Task ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        LoadStatistics stats,
        CancellationToken cancellationToken,
        Func<HttpResponseMessage, CancellationToken, Task>? onResponse = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await send(cancellationToken);
            if (onResponse is not null)
                await onResponse(response, cancellationToken);
            else
                await response.Content.CopyToAsync(Stream.Null, cancellationToken);

            sw.Stop();
            if (response.IsSuccessStatusCode)
                stats.RecordSuccess(sw.ElapsedMilliseconds);
            else
                stats.RecordError(sw.ElapsedMilliseconds, FormatStatus(response.StatusCode));
        }
        catch (Exception ex)
        {
            sw.Stop();
            stats.RecordError(sw.ElapsedMilliseconds, FormatException(ex));
        }
    }

    private static string FormatStatus(HttpStatusCode statusCode) =>
        $"HTTP {(int)statusCode} {statusCode}";

    private static string FormatException(Exception ex)
    {
        var root = ex.GetBaseException();
        var text = $"{root.GetType().Name}: {root.Message}";
        return text.Length <= 120 ? text : text[..117] + "...";
    }
}
