using System.Collections.Concurrent;
using System.Diagnostics;

namespace ConferenceHallBooking.LoadTesting;

internal sealed class LoadStatistics
{
    private readonly ConcurrentBag<long> _latenciesMs = [];
    private long _success;
    private long _errors;

    public void RecordSuccess(long elapsedMs)
    {
        Interlocked.Increment(ref _success);
        _latenciesMs.Add(elapsedMs);
    }

    public void RecordError(long elapsedMs)
    {
        Interlocked.Increment(ref _errors);
        _latenciesMs.Add(elapsedMs);
    }

    public LoadResult Snapshot(string engine, int taskCount, int concurrency, TimeSpan totalElapsed)
    {
        var latencies = _latenciesMs.ToArray();
        double avg = latencies.Length == 0 ? 0 : latencies.Average();
        long min = latencies.Length == 0 ? 0 : latencies.Min();
        long max = latencies.Length == 0 ? 0 : latencies.Max();

        return new LoadResult(
            Engine: engine,
            TaskCount: taskCount,
            Concurrency: concurrency,
            TotalElapsed: totalElapsed,
            AverageMs: avg,
            MinMs: min,
            MaxMs: max,
            SuccessCount: Interlocked.Read(ref _success),
            ErrorCount: Interlocked.Read(ref _errors));
    }
}

internal sealed record LoadResult(
    string Engine,
    int TaskCount,
    int Concurrency,
    TimeSpan TotalElapsed,
    double AverageMs,
    long MinMs,
    long MaxMs,
    long SuccessCount,
    long ErrorCount)
{
    public void Print()
    {
        Console.WriteLine();
        Console.WriteLine($"=== Результат: engine={Engine}, tasks={TaskCount}, concurrency={Concurrency} ===");
        Console.WriteLine($"Загальний час:           {TotalElapsed.TotalSeconds:F2} с ({TotalElapsed.TotalMilliseconds:F0} мс)");
        Console.WriteLine($"Середній час відповіді:  {AverageMs:F2} мс");
        Console.WriteLine($"Мін. час відповіді:      {MinMs} мс");
        Console.WriteLine($"Макс. час відповіді:     {MaxMs} мс");
        Console.WriteLine($"Успішних запитів:        {SuccessCount}");
        Console.WriteLine($"Запитів з помилками:     {ErrorCount}");
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
            $"{"Engine",-12} {"Conc",6} {"Total(s)",10} {"Avg(ms)",10} {"Min",8} {"Max",8} {"OK",8} {"Err",8}");
        Console.WriteLine(new string('-', 78));

        foreach (var r in results)
        {
            Console.WriteLine(
                $"{r.Engine,-12} {r.Concurrency,6} {r.TotalElapsed.TotalSeconds,10:F2} {r.AverageMs,10:F2} {r.MinMs,8} {r.MaxMs,8} {r.SuccessCount,8} {r.ErrorCount,8}");
        }
    }
}

internal static class TimedRequest
{
    public static async Task ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        LoadStatistics stats,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await send(cancellationToken);
            sw.Stop();
            if (response.IsSuccessStatusCode)
                stats.RecordSuccess(sw.ElapsedMilliseconds);
            else
                stats.RecordError(sw.ElapsedMilliseconds);
        }
        catch
        {
            sw.Stop();
            stats.RecordError(sw.ElapsedMilliseconds);
        }
    }
}
