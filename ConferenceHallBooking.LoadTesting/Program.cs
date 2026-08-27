using ConferenceHallBooking.LoadTesting;

try
{
    if (args.Any(a => a is "--help" or "-h"))
    {
        LoadTestOptions.PrintHelp();
        return 0;
    }

    var options = LoadTestOptions.Parse(args);
    var engine = LoadEngineFactory.Create(options.Engine);

    Console.WriteLine("Conference Hall Booking — Load Testing (dev)");
    Console.WriteLine($"Base URL:     {options.BaseUrl}");
    Console.WriteLine($"Engine:       {engine.Name}");
    Console.WriteLine($"Tasks:        {options.TaskCount}");
    Console.WriteLine($"Concurrency:  {string.Join(", ", options.ConcurrencyLevels)}");
    Console.WriteLine();

    using var http = new HttpClient
    {
        BaseAddress = new Uri(options.BaseUrl),
        Timeout = TimeSpan.FromSeconds(60)
    };
    http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var workload = await ApiWorkload.CreateAsync(http, cts.Token);
    var results = new List<LoadResult>();

    foreach (var concurrency in options.ConcurrencyLevels)
    {
        Console.WriteLine();
        Console.WriteLine($">>> Старт: {options.TaskCount} tasks / {concurrency} concurrent ({engine.Name})...");
        var result = await engine.RunAsync(workload, options.TaskCount, concurrency, cts.Token);
        result.Print();
        results.Add(result);
    }

    LoadResultTable.PrintComparison(results);
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
    Console.Error.WriteLine($"Помилка: {ex.Message}");
    return 1;
}
