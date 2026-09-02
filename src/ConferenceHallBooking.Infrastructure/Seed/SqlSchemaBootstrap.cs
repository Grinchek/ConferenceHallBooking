using ConferenceHallBooking.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ConferenceHallBooking.Infrastructure.Seed;

internal static class SqlSchemaBootstrap
{
    public static async Task EnsureSchemaAndTablesAsync(
        ISqlConnectionFactory connectionFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, $"""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{SqlSchema.Name}')
                EXEC(N'CREATE SCHEMA [{SqlSchema.Name}]');
            """, cancellationToken);

        if (await HallsTableExistsAsync(connection, cancellationToken))
        {
            await EnsureBookingHallNameColumnAsync(connection, logger, cancellationToken);
            return;
        }

        await CreateTablesAsync(connection, cancellationToken);
        logger.LogInformation("Створено таблиці в схемі {Schema}.", SqlSchema.Name);
    }

    public static async Task<bool> HasAnyHallsAsync(
        ISqlConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM {SqlSchema.Table("Halls")}
            ) THEN 1 ELSE 0 END
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    private static async Task<bool> HallsTableExistsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT CASE
                WHEN OBJECT_ID(N'{SqlSchema.Table("Halls")}', N'U') IS NOT NULL THEN 1
                ELSE 0
            END
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    private static async Task CreateTablesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, $"""
            CREATE TABLE {SqlSchema.Table("Halls")} (
                [Id] uniqueidentifier NOT NULL,
                [Name] nvarchar(100) NOT NULL,
                [Capacity] int NOT NULL,
                [BaseHourlyRate] decimal(18, 2) NOT NULL,
                [IsDeleted] bit NOT NULL,
                [CreatedAtUtc] datetime2 NOT NULL,
                [UpdatedAtUtc] datetime2 NULL,
                CONSTRAINT [PK_Halls] PRIMARY KEY ([Id])
            );

            CREATE INDEX [IX_Halls_Name] ON {SqlSchema.Table("Halls")} ([Name]);

            CREATE TABLE {SqlSchema.Table("HallServices")} (
                [Id] uniqueidentifier NOT NULL,
                [HallId] uniqueidentifier NOT NULL,
                [Name] nvarchar(100) NOT NULL,
                [Price] decimal(18, 2) NOT NULL,
                CONSTRAINT [PK_HallServices] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_HallServices_Halls_HallId] FOREIGN KEY ([HallId])
                    REFERENCES {SqlSchema.Table("Halls")} ([Id]) ON DELETE CASCADE
            );

            CREATE TABLE {SqlSchema.Table("Bookings")} (
                [Id] uniqueidentifier NOT NULL,
                [HallId] uniqueidentifier NOT NULL,
                [HallName] nvarchar(100) NOT NULL,
                [StartUtc] datetime2 NOT NULL,
                [EndUtc] datetime2 NOT NULL,
                [DurationHours] decimal(18, 2) NOT NULL,
                [CustomerName] nvarchar(200) NULL,
                [HallRentalCost] decimal(18, 2) NOT NULL,
                [ServicesCost] decimal(18, 2) NOT NULL,
                [TotalCost] decimal(18, 2) NOT NULL,
                [IsCancelled] bit NOT NULL,
                [CreatedAtUtc] datetime2 NOT NULL,
                CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_Bookings_Halls_HallId] FOREIGN KEY ([HallId])
                    REFERENCES {SqlSchema.Table("Halls")} ([Id])
            );

            CREATE INDEX [IX_Bookings_HallId_StartUtc_EndUtc]
                ON {SqlSchema.Table("Bookings")} ([HallId], [StartUtc], [EndUtc]);

            CREATE TABLE {SqlSchema.Table("BookingServiceItems")} (
                [Id] uniqueidentifier NOT NULL,
                [BookingId] uniqueidentifier NOT NULL,
                [Name] nvarchar(100) NOT NULL,
                [Price] decimal(18, 2) NOT NULL,
                CONSTRAINT [PK_BookingServiceItems] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_BookingServiceItems_Bookings_BookingId] FOREIGN KEY ([BookingId])
                    REFERENCES {SqlSchema.Table("Bookings")} ([Id]) ON DELETE CASCADE
            );
            """, cancellationToken);
    }

    private static async Task EnsureBookingHallNameColumnAsync(
        SqlConnection connection,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, $"""
            IF COL_LENGTH(N'{SqlSchema.Table("Bookings")}', N'HallName') IS NULL
            BEGIN
                ALTER TABLE {SqlSchema.Table("Bookings")}
                ADD [HallName] nvarchar(100) NOT NULL
                    CONSTRAINT [DF_Bookings_HallName] DEFAULT (N'');
            END
            """, cancellationToken);

        await ExecuteAsync(connection, $"""
            UPDATE b
            SET b.[HallName] = COALESCE(NULLIF(h.[Name], N''), N'Unknown')
            FROM {SqlSchema.Table("Bookings")} b
            LEFT JOIN {SqlSchema.Table("Halls")} h ON h.[Id] = b.[HallId]
            WHERE b.[HallName] = N'' OR b.[HallName] IS NULL
            """, cancellationToken);

        logger.LogInformation("Перевірено колонку HallName у таблиці Bookings.");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
