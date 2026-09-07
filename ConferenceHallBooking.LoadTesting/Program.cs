using ConferenceHallBooking.LoadTesting;

try
{
    if (args.Any(a => a is "--help" or "-h"))
    {
        LoadTestOptions.PrintHelp();
        return 0;
    }

    var options = LoadTestOptions.Parse(args);

    Console.WriteLine("Conference Hall Booking — Load Testing (dev)");
    Console.WriteLine($"Base URL:     {options.BaseUrl}");
    Console.WriteLine($"Tasks:        {options.TaskCount}");
    Console.WriteLine($"Concurrency:  {string.Join(", ", options.ConcurrencyLevels)}");
    Console.WriteLine($"Cleanup:      {(options.Cleanup ? "on (після прогонів)" : "off")}");
    Console.WriteLine("Throttle:     SemaphoreSlim + Task.WhenAll");
    Console.WriteLine();

    using var http = CreateHttpClient(options);
    http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var workload = await ApiWorkload.CreateAsync(http, cts.Token);
    var results = new List<LoadResult>();

    try
    {
        var levels = options.ConcurrencyLevels;
        for (var i = 0; i < levels.Count; i++)
        {
            var concurrency = levels[i];
            Console.WriteLine();
            Console.WriteLine($">>> Старт: {options.TaskCount} tasks / {concurrency} concurrent...");
            var result = await LoadRunner.RunAsync(workload, options.TaskCount, concurrency, cts.Token);
            result.Print();
            results.Add(result);

            if (i < levels.Count - 1)
            {
                Console.WriteLine("Пауза 2 с перед наступним сценарієм...");
                await Task.Delay(2000, cts.Token);
                await EnsureApiAliveAsync(http, cts.Token);
            }
        }

        LoadResultTable.PrintComparison(results);
    }
    finally
    {
        if (options.Cleanup && !cts.IsCancellationRequested)
        {
            Console.WriteLine();
            await EnsureApiAliveAsync(http, cts.Token);
            await workload.CleanupAsync(cts.Token);
        }
        else if (options.Cleanup && cts.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.WriteLine("Cleanup пропущено (скасування). За потреби: cleanup-test-halls.sql");
        }
    }

    return 0;
}
catch (HelpRequestedException)
{
    LoadTestOptions.PrintHelp();
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine();
    Console.WriteLine("Перервано користувачем.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Помилка: {ex.GetBaseException().Message}");
    return 1;
}

static HttpClient CreateHttpClient(LoadTestOptions options)
{
    var baseUri = new Uri(options.BaseUrl);
    var handler = new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        MaxConnectionsPerServer = 500
    };

    if (baseUri.Host is "localhost" or "127.0.0.1")
    {
        handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
    }

    return new HttpClient(handler)
    {
        BaseAddress = baseUri,
        Timeout = TimeSpan.FromSeconds(60)
    };
}

static async Task EnsureApiAliveAsync(HttpClient http, CancellationToken cancellationToken)
{
    try
    {
        using var response = await http.GetAsync("/health", cancellationToken);
        await response.Content.CopyToAsync(Stream.Null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            Console.WriteLine($"⚠ /health → {(int)response.StatusCode}. Наступний крок може бути невалідним.");
        else
            Console.WriteLine("API alive (/health OK).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ API не відповідає на /health: {ex.GetBaseException().Message}");
    }
}
