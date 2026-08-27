using System.Diagnostics;

namespace ConferenceHallBooking.LoadTesting;

internal interface ILoadEngine
{
    string Name { get; }

    Task<LoadResult> RunAsync(
        ApiWorkload workload,
        int taskCount,
        int concurrency,
        CancellationToken cancellationToken);
}

/// <summary>
/// Створює taskCount асинхронних задач і обмежує одночасні HTTP через SemaphoreSlim.
/// </summary>
internal sealed class SemaphoreLoadEngine : ILoadEngine
{
    public string Name => "semaphore";

    public async Task<LoadResult> RunAsync(
        ApiWorkload workload,
        int taskCount,
        int concurrency,
        CancellationToken cancellationToken)
    {
        var stats = new LoadStatistics();
        using var gate = new SemaphoreSlim(concurrency, concurrency);
        var totalSw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, taskCount).Select(async i =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                await workload.RunOneAsync(i, stats, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        totalSw.Stop();

        return stats.Snapshot(Name, taskCount, concurrency, totalSw.Elapsed);
    }
}

/// <summary>
/// Альтернативний режим: Parallel.ForEachAsync з MaxDegreeOfParallelism.
/// </summary>
internal sealed class ParallelForEachLoadEngine : ILoadEngine
{
    public string Name => "foreach";

    public async Task<LoadResult> RunAsync(
        ApiWorkload workload,
        int taskCount,
        int concurrency,
        CancellationToken cancellationToken)
    {
        var stats = new LoadStatistics();
        var totalSw = Stopwatch.StartNew();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, taskCount),
            options,
            async (i, ct) => await workload.RunOneAsync(i, stats, ct));

        totalSw.Stop();
        return stats.Snapshot(Name, taskCount, concurrency, totalSw.Elapsed);
    }
}

internal static class LoadEngineFactory
{
    public static ILoadEngine Create(LoadEngineKind kind) => kind switch
    {
        LoadEngineKind.Semaphore => new SemaphoreLoadEngine(),
        LoadEngineKind.ForEach => new ParallelForEachLoadEngine(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
