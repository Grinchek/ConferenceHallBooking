namespace ConferenceHallBooking.LoadTesting;

internal enum LoadEngineKind
{
    Semaphore,
    ForEach
}

internal sealed class LoadTestOptions
{
    public string BaseUrl { get; init; } = "http://localhost:5105";
    public string ApiKey { get; init; } = "dev-api-key-change-me";
    public int TaskCount { get; init; } = 1000;
    public int Concurrency { get; init; } = 10;
    public int[] Scenarios { get; init; } = [];
    public LoadEngineKind Engine { get; init; } = LoadEngineKind.Semaphore;

    public IReadOnlyList<int> ConcurrencyLevels =>
        Scenarios.Length > 0 ? Scenarios : [Concurrency];

    public static LoadTestOptions Parse(string[] args)
    {
        var baseUrl = "http://localhost:5105";
        var apiKey = "dev-api-key-change-me";
        var taskCount = 1000;
        var concurrency = 10;
        int[] scenarios = [];
        var engine = LoadEngineKind.Semaphore;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next() => i + 1 < args.Length
                ? args[++i]
                : throw new ArgumentException($"Для {arg} потрібне значення.");

            switch (arg.ToLowerInvariant())
            {
                case "--base-url":
                    baseUrl = Next().TrimEnd('/');
                    break;
                case "--api-key":
                    apiKey = Next();
                    break;
                case "--tasks":
                    taskCount = ParsePositiveInt(Next(), "--tasks");
                    break;
                case "--concurrency":
                    concurrency = ParsePositiveInt(Next(), "--concurrency");
                    break;
                case "--scenarios":
                    scenarios = Next()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => ParsePositiveInt(s, "--scenarios"))
                        .ToArray();
                    if (scenarios.Length == 0)
                        throw new ArgumentException("--scenarios має містити хоча б одне число.");
                    break;
                case "--engine":
                    engine = Next().ToLowerInvariant() switch
                    {
                        "semaphore" => LoadEngineKind.Semaphore,
                        "foreach" => LoadEngineKind.ForEach,
                        _ => throw new ArgumentException("--engine: очікується semaphore або foreach.")
                    };
                    break;
                case "--help":
                case "-h":
                    throw new HelpRequestedException();
                default:
                    throw new ArgumentException($"Невідомий аргумент: {arg}");
            }
        }

        return new LoadTestOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            TaskCount = taskCount,
            Concurrency = concurrency,
            Scenarios = scenarios,
            Engine = engine
        };
    }

    private static int ParsePositiveInt(string value, string name)
    {
        if (!int.TryParse(value, out var n) || n <= 0)
            throw new ArgumentException($"{name} має бути додатним цілим числом.");
        return n;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            Conference Hall Booking — навантажувальне тестування (dev)

            Використання:
              dotnet run -- [опції]

            Опції:
              --base-url <url>       База API (default: http://localhost:5105)
              --api-key <key>        X-Api-Key (default: dev-api-key-change-me)
              --tasks <n>            Кількість асинхронних задач (default: 1000)
              --concurrency <n>      Одночасних HTTP-запитів (default: 10)
              --scenarios <a,b,c>    Кілька рівнів concurrency, напр. 10,50,100
              --engine <name>        semaphore (default) | foreach
              --help, -h             Довідка

            Приклади:
              dotnet run -- --tasks 1000 --concurrency 10
              dotnet run -- --tasks 1000 --scenarios 10,50,100
              dotnet run -- --engine foreach --scenarios 10,50,100
            """);
    }
}

internal sealed class HelpRequestedException : Exception;
