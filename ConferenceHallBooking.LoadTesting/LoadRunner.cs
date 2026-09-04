using System.Diagnostics;

namespace ConferenceHallBooking.LoadTesting;

/// <summary>
/// Створює taskCount асинхронних задач і обмежує одночасні HTTP через SemaphoreSlim.
/// </summary>
internal static class LoadRunner
{
    public static async Task<LoadResult> RunAsync(
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

        return stats.Snapshot(taskCount, concurrency, totalSw.Elapsed);
    }
}
