using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConferenceHallBooking.Infrastructure.Seed;

/// <summary>
/// Початкові дані згідно з умовами тестового завдання.
/// </summary>
public static class DatabaseSeeder
{
    private static readonly (string Name, decimal Price)[] DefaultServices =
    [
        ("Проєктор", 500m),
        ("Wi-Fi", 300m),
        ("Звук", 700m)
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var hallRepository = scope.ServiceProvider.GetRequiredService<IHallRepository>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        await SqlSchemaBootstrap.EnsureSchemaAndTablesAsync(connectionFactory, logger);

        if (await SqlSchemaBootstrap.HasAnyHallsAsync(connectionFactory))
        {
            logger.LogInformation("База вже містить дані — seed пропущено.");
            return;
        }

        var halls = new[]
        {
            CreateHall("Зал А", 50, 2000m),
            CreateHall("Зал B", 100, 3500m),
            CreateHall("Зал C", 30, 1500m)
        };

        foreach (var hall in halls)
            await hallRepository.AddAsync(hall);

        logger.LogInformation("Початкові зали та послуги успішно додано.");
    }

    private static Hall CreateHall(string name, int capacity, decimal rate)
    {
        var services = DefaultServices.Select(s => new HallService(s.Name, s.Price));
        return new Hall(name, capacity, rate, services);
    }
}
