using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConferenceHallBooking.Infrastructure.Seed;

/// <summary>
/// Початкові дані згідно з умовами тестового завдання.
/// </summary>
public static class DatabaseSeeder
{
    private const string SchemaName = "IGrinSchema";

    private static readonly (string Name, decimal Price)[] DefaultServices =
    [
        ("Проєктор", 500m),
        ("Wi-Fi", 300m),
        ("Звук", 700m)
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        // EnsureCreated нічого не робить, якщо БД уже існує (типово для спільної Azure SQL).
        await EnsureSchemaAndTablesAsync(db, logger);

        if (await db.Halls.IgnoreQueryFilters().AnyAsync())
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

        await db.Halls.AddRangeAsync(halls);
        await db.SaveChangesAsync();

        logger.LogInformation("Початкові зали та послуги успішно додано.");
    }

    private static async Task EnsureSchemaAndTablesAsync(AppDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync($"""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{SchemaName}')
                EXEC(N'CREATE SCHEMA [{SchemaName}]');
            """);

        var hallsTableExists = await db.Database
            .SqlQueryRaw<int>($"""
                SELECT CASE
                    WHEN OBJECT_ID(N'[{SchemaName}].[Halls]', N'U') IS NOT NULL THEN 1
                    ELSE 0
                END AS [Value]
                """)
            .SingleAsync() == 1;

        if (hallsTableExists)
        {
            await EnsureBookingHallNameColumnAsync(db, logger);
            return;
        }

        var creator = db.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();
        logger.LogInformation("Створено таблиці в схемі {Schema}.", SchemaName);
    }

    private static async Task EnsureBookingHallNameColumnAsync(AppDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync($"""
            IF COL_LENGTH(N'[{SchemaName}].[Bookings]', N'HallName') IS NULL
            BEGIN
                ALTER TABLE [{SchemaName}].[Bookings]
                ADD [HallName] nvarchar(100) NOT NULL
                    CONSTRAINT [DF_Bookings_HallName] DEFAULT (N'');

                UPDATE b
                SET b.[HallName] = COALESCE(NULLIF(h.[Name], N''), N'Unknown')
                FROM [{SchemaName}].[Bookings] b
                LEFT JOIN [{SchemaName}].[Halls] h ON h.[Id] = b.[HallId];
            END
            """);

        logger.LogInformation("Перевірено колонку HallName у таблиці Bookings.");
    }

    private static Hall CreateHall(string name, int capacity, decimal rate)
    {
        var services = DefaultServices.Select(s => new HallService(s.Name, s.Price));
        return new Hall(name, capacity, rate, services);
    }
}
