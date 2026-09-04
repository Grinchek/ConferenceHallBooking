namespace ConferenceHallBooking.LoadTesting;

internal sealed class LoadTestOptions
{
    public string BaseUrl { get; init; } = "http://localhost:5105";
    public string ApiKey { get; init; } = "dev-api-key-change-me";
    public int TaskCount { get; init; } = 1000;
    public int Concurrency { get; init; } = 10;
    public int[] Scenarios { get; init; } = [];
    public bool Cleanup { get; init; } = true;

    public IReadOnlyList<int> ConcurrencyLevels =>
        Scenarios.Length > 0 ? Scenarios : [Concurrency];

    public static LoadTestOptions Parse(string[] args)
    {
        var baseUrl = "http://localhost:5105";
        var apiKey = "dev-api-key-change-me";
        var taskCount = 1000;
        var concurrency = 10;
        int[] scenarios = [];
        var cleanup = true;

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
                case "--cleanup":
                    cleanup = true;
                    break;
                case "--no-cleanup":
                    cleanup = false;
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
            Cleanup = cleanup
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
              --cleanup              Soft-delete тестових залів після прогонів (default)
              --no-cleanup           Не прибирати тестові зали
              --help, -h             Довідка

            Паралельність: N задач (Task.WhenAll) + SemaphoreSlim(concurrency).
            Мікс: переважно GET; POST/PUT залів рідше. PUT лише по створених у прогоні.
            Cleanup: DELETE через API (soft-delete), seed-зали А/B/C не чіпає.

            Приклади:
              dotnet run -- --tasks 1000 --concurrency 10
              dotnet run -- --tasks 1000 --scenarios 10,50,100
              dotnet run -- --tasks 1000 --concurrency 10 --no-cleanup
            """);
    }
}

internal sealed class HelpRequestedException : Exception;
