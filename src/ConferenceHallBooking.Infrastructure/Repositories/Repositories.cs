using System.Data;
using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ConferenceHallBooking.Infrastructure.Repositories;

public sealed class HallRepository : IHallRepository
{
    private readonly SqlSession _session;

    public HallRepository(SqlSession session) =>
        _session = session;

    public async Task<Hall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = await _session.CreateCommandAsync(cancellationToken);
        command.CommandText = $"""
            SELECT Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc
            FROM {SqlSchema.Table("Halls")}
            WHERE Id = @Id AND IsDeleted = 0
            """;
        command.Parameters.Add(new SqlParameter("@Id", id));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadHall(reader);
    }

    public async Task<Hall?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        Hall? hall;
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc
                FROM {SqlSchema.Table("Halls")}
                WHERE Id = @Id AND IsDeleted = 0
                """;
            command.Parameters.Add(new SqlParameter("@Id", id));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            hall = ReadHall(reader);
        }

        var services = new List<HallService>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, HallId, Name, Price
                FROM {SqlSchema.Table("HallServices")}
                WHERE HallId = @HallId
                ORDER BY Name
                """;
            command.Parameters.Add(new SqlParameter("@HallId", id));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                services.Add(ReadHallService(reader));
        }

        hall.RestoreServices(services);
        return hall;
    }

    public async Task<IReadOnlyList<Hall>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var halls = new List<Hall>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc
                FROM {SqlSchema.Table("Halls")}
                WHERE IsDeleted = 0
                ORDER BY Name
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                halls.Add(ReadHall(reader));
        }

        if (halls.Count == 0)
            return halls;

        var servicesByHallId = new Dictionary<Guid, List<HallService>>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT s.Id, s.HallId, s.Name, s.Price
                FROM {SqlSchema.Table("HallServices")} AS s
                INNER JOIN {SqlSchema.Table("Halls")} AS h ON h.Id = s.HallId
                WHERE h.IsDeleted = 0
                ORDER BY s.Name
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var service = ReadHallService(reader);
                if (!servicesByHallId.TryGetValue(service.HallId, out var list))
                {
                    list = [];
                    servicesByHallId[service.HallId] = list;
                }

                list.Add(service);
            }
        }

        foreach (var hall in halls)
        {
            if (servicesByHallId.TryGetValue(hall.Id, out var services))
                hall.RestoreServices(services);
        }

        return halls;
    }

    private static Hall ReadHall(SqlDataReader reader) =>
        Hall.Restore(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetDecimal(3),
            reader.GetBoolean(4),
            reader.GetDateTime(5),
            reader.IsDBNull(6) ? null : reader.GetDateTime(6));

    private static HallService ReadHallService(SqlDataReader reader) =>
        HallService.Restore(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetDecimal(3));

    public async Task<IReadOnlyList<Hall>> SearchAvailableAsync(
        DateTime start,
        DateTime end,
        int requiredCapacity,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var candidates = new List<Hall>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc
                FROM {SqlSchema.Table("Halls")}
                WHERE IsDeleted = 0 AND Capacity >= @RequiredCapacity
                """;
            command.Parameters.Add(new SqlParameter("@RequiredCapacity", requiredCapacity));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                candidates.Add(ReadHall(reader));
        }

        if (candidates.Count == 0)
            return candidates;

        var busyHallIds = new HashSet<Guid>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT DISTINCT HallId
                FROM {SqlSchema.Table("Bookings")}
                WHERE IsCancelled = 0
                  AND StartUtc < @End
                  AND EndUtc > @Start
                """;
            command.Parameters.Add(new SqlParameter("@End", end));
            command.Parameters.Add(new SqlParameter("@Start", start));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                busyHallIds.Add(reader.GetGuid(0));
        }

        var available = candidates
            .Where(h => !busyHallIds.Contains(h.Id))
            .OrderBy(h => h.BaseHourlyRate)
            .ToList();

        if (available.Count == 0)
            return available;

        var availableIds = available.Select(h => h.Id).ToHashSet();
        var servicesByHallId = new Dictionary<Guid, List<HallService>>();

        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT s.Id, s.HallId, s.Name, s.Price
                FROM {SqlSchema.Table("HallServices")} AS s
                INNER JOIN {SqlSchema.Table("Halls")} AS h ON h.Id = s.HallId
                WHERE h.IsDeleted = 0
                ORDER BY s.Name
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var service = ReadHallService(reader);
                if (!availableIds.Contains(service.HallId))
                    continue;

                if (!servicesByHallId.TryGetValue(service.HallId, out var list))
                {
                    list = [];
                    servicesByHallId[service.HallId] = list;
                }

                list.Add(service);
            }
        }

        foreach (var hall in available)
        {
            if (servicesByHallId.TryGetValue(hall.Id, out var services))
                hall.RestoreServices(services);
        }

        return available;
    }

    public async Task AddAsync(Hall hall, CancellationToken cancellationToken = default)
    {
        await _session.ExecuteTransactionalAsync(async ct =>
        {
            var connection = await _session.GetOpenConnectionAsync(ct);

            await using (var command = _session.CreateCommand(connection))
            {
                command.CommandText = $"""
                    INSERT INTO {SqlSchema.Table("Halls")}
                        (Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
                    VALUES
                        (@Id, @Name, @Capacity, @BaseHourlyRate, @IsDeleted, @CreatedAtUtc, @UpdatedAtUtc)
                    """;
                command.Parameters.Add(new SqlParameter("@Id", hall.Id));
                command.Parameters.Add(new SqlParameter("@Name", hall.Name));
                command.Parameters.Add(new SqlParameter("@Capacity", hall.Capacity));
                command.Parameters.Add(new SqlParameter("@BaseHourlyRate", hall.BaseHourlyRate));
                command.Parameters.Add(new SqlParameter("@IsDeleted", hall.IsDeleted));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", hall.CreatedAtUtc));
                command.Parameters.Add(new SqlParameter("@UpdatedAtUtc", (object?)hall.UpdatedAtUtc ?? DBNull.Value));

                await command.ExecuteNonQueryAsync(ct);
            }

            foreach (var service in hall.Services)
            {
                await using var command = _session.CreateCommand(connection);
                command.CommandText = $"""
                    INSERT INTO {SqlSchema.Table("HallServices")}
                        (Id, HallId, Name, Price)
                    VALUES
                        (@Id, @HallId, @Name, @Price)
                    """;
                command.Parameters.Add(new SqlParameter("@Id", service.Id));
                command.Parameters.Add(new SqlParameter("@HallId", hall.Id));
                command.Parameters.Add(new SqlParameter("@Name", service.Name));
                command.Parameters.Add(new SqlParameter("@Price", service.Price));

                await command.ExecuteNonQueryAsync(ct);
            }
        }, IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task UpdateAsync(Hall hall, CancellationToken cancellationToken = default)
    {
        await using var command = await _session.CreateCommandAsync(cancellationToken);
        command.CommandText = $"""
            UPDATE {SqlSchema.Table("Halls")}
            SET Name = @Name,
                Capacity = @Capacity,
                BaseHourlyRate = @BaseHourlyRate,
                IsDeleted = @IsDeleted,
                UpdatedAtUtc = @UpdatedAtUtc
            WHERE Id = @Id
            """;
        command.Parameters.Add(new SqlParameter("@Id", hall.Id));
        command.Parameters.Add(new SqlParameter("@Name", hall.Name));
        command.Parameters.Add(new SqlParameter("@Capacity", hall.Capacity));
        command.Parameters.Add(new SqlParameter("@BaseHourlyRate", hall.BaseHourlyRate));
        command.Parameters.Add(new SqlParameter("@IsDeleted", hall.IsDeleted));
        command.Parameters.Add(new SqlParameter("@UpdatedAtUtc", (object?)hall.UpdatedAtUtc ?? DBNull.Value));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetServicesAsync(
        Guid hallId,
        IEnumerable<(string Name, decimal Price)> services,
        CancellationToken cancellationToken = default)
    {
        var distinct = services
            .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        await _session.ExecuteTransactionalAsync(async ct =>
        {
            var connection = await _session.GetOpenConnectionAsync(ct);

            await using (var command = _session.CreateCommand(connection))
            {
                command.CommandText = $"""
                    DELETE FROM {SqlSchema.Table("HallServices")}
                    WHERE HallId = @HallId
                    """;
                command.Parameters.Add(new SqlParameter("@HallId", hallId));

                await command.ExecuteNonQueryAsync(ct);
            }

            foreach (var service in distinct)
            {
                var entity = new HallService(service.Name, service.Price, hallId);

                await using var command = _session.CreateCommand(connection);
                command.CommandText = $"""
                    INSERT INTO {SqlSchema.Table("HallServices")}
                        (Id, HallId, Name, Price)
                    VALUES
                        (@Id, @HallId, @Name, @Price)
                    """;
                command.Parameters.Add(new SqlParameter("@Id", entity.Id));
                command.Parameters.Add(new SqlParameter("@HallId", hallId));
                command.Parameters.Add(new SqlParameter("@Name", entity.Name));
                command.Parameters.Add(new SqlParameter("@Price", entity.Price));

                await command.ExecuteNonQueryAsync(ct);
            }
        }, IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = await _session.CreateCommandAsync(cancellationToken);
        command.CommandText = $"""
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM {SqlSchema.Table("Halls")}
                WHERE IsDeleted = 0
                  AND LOWER(Name) = LOWER(@Name)
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            ) THEN 1 ELSE 0 END
            """;

        command.Parameters.Add(new SqlParameter("@Name", name.Trim()));
        command.Parameters.Add(new SqlParameter("@ExcludeId", (object?)excludeId ?? DBNull.Value));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }
}

public sealed class BookingRepository : IBookingRepository
{
    private readonly SqlSession _session;

    public BookingRepository(SqlSession session) =>
        _session = session;

    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        Booking? booking;
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, HallId, HallName, StartUtc, EndUtc, DurationHours, CustomerName,
                       HallRentalCost, ServicesCost, TotalCost, IsCancelled, CreatedAtUtc
                FROM {SqlSchema.Table("Bookings")}
                WHERE Id = @Id
                """;
            command.Parameters.Add(new SqlParameter("@Id", id));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            booking = ReadBooking(reader);
        }

        var services = new List<BookingServiceItem>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, BookingId, Name, Price
                FROM {SqlSchema.Table("BookingServiceItems")}
                WHERE BookingId = @BookingId
                ORDER BY Name
                """;
            command.Parameters.Add(new SqlParameter("@BookingId", id));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                services.Add(ReadBookingServiceItem(reader));
        }

        booking.RestoreSelectedServices(services);
        return booking;
    }

    public async Task<IReadOnlyList<Booking>> GetAllAsync(bool includeCancelled = false, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var bookings = new List<Booking>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = includeCancelled
                ? $"""
                    SELECT Id, HallId, HallName, StartUtc, EndUtc, DurationHours, CustomerName,
                           HallRentalCost, ServicesCost, TotalCost, IsCancelled, CreatedAtUtc
                    FROM {SqlSchema.Table("Bookings")}
                    ORDER BY CreatedAtUtc DESC
                    """
                : $"""
                    SELECT Id, HallId, HallName, StartUtc, EndUtc, DurationHours, CustomerName,
                           HallRentalCost, ServicesCost, TotalCost, IsCancelled, CreatedAtUtc
                    FROM {SqlSchema.Table("Bookings")}
                    WHERE IsCancelled = 0
                    ORDER BY CreatedAtUtc DESC
                    """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                bookings.Add(ReadBooking(reader));
        }

        if (bookings.Count == 0)
            return bookings;

        var bookingIds = bookings.Select(b => b.Id).ToHashSet();
        var servicesByBookingId = new Dictionary<Guid, List<BookingServiceItem>>();

        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, BookingId, Name, Price
                FROM {SqlSchema.Table("BookingServiceItems")}
                ORDER BY Name
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var service = ReadBookingServiceItem(reader);
                if (!bookingIds.Contains(service.BookingId))
                    continue;

                if (!servicesByBookingId.TryGetValue(service.BookingId, out var list))
                {
                    list = [];
                    servicesByBookingId[service.BookingId] = list;
                }

                list.Add(service);
            }
        }

        foreach (var booking in bookings)
        {
            if (servicesByBookingId.TryGetValue(booking.Id, out var services))
                booking.RestoreSelectedServices(services);
        }

        return bookings;
    }

    public async Task<IReadOnlyList<Booking>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var bookings = new List<Booking>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, HallId, HallName, StartUtc, EndUtc, DurationHours, CustomerName,
                       HallRentalCost, ServicesCost, TotalCost, IsCancelled, CreatedAtUtc
                FROM {SqlSchema.Table("Bookings")}
                WHERE IsCancelled = 0
                  AND StartUtc < @To
                  AND EndUtc > @From
                ORDER BY CreatedAtUtc DESC
                """;
            command.Parameters.Add(new SqlParameter("@From", from));
            command.Parameters.Add(new SqlParameter("@To", to));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                bookings.Add(ReadBooking(reader));
        }

        if (bookings.Count == 0)
            return bookings;

        var bookingIds = bookings.Select(b => b.Id).ToHashSet();
        var servicesByBookingId = new Dictionary<Guid, List<BookingServiceItem>>();

        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, BookingId, Name, Price
                FROM {SqlSchema.Table("BookingServiceItems")}
                ORDER BY Name
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var service = ReadBookingServiceItem(reader);
                if (!bookingIds.Contains(service.BookingId))
                    continue;

                if (!servicesByBookingId.TryGetValue(service.BookingId, out var list))
                {
                    list = [];
                    servicesByBookingId[service.BookingId] = list;
                }

                list.Add(service);
            }
        }

        foreach (var booking in bookings)
        {
            if (servicesByBookingId.TryGetValue(booking.Id, out var services))
                booking.RestoreSelectedServices(services);
        }

        return bookings;
    }

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await _session.ExecuteTransactionalAsync(async ct =>
        {
            var connection = await _session.GetOpenConnectionAsync(ct);

            await using (var command = _session.CreateCommand(connection))
            {
                command.CommandText = $"""
                    INSERT INTO {SqlSchema.Table("Bookings")}
                        (Id, HallId, HallName, StartUtc, EndUtc, DurationHours, CustomerName,
                         HallRentalCost, ServicesCost, TotalCost, IsCancelled, CreatedAtUtc)
                    VALUES
                        (@Id, @HallId, @HallName, @StartUtc, @EndUtc, @DurationHours, @CustomerName,
                         @HallRentalCost, @ServicesCost, @TotalCost, @IsCancelled, @CreatedAtUtc)
                    """;
                command.Parameters.Add(new SqlParameter("@Id", booking.Id));
                command.Parameters.Add(new SqlParameter("@HallId", booking.HallId));
                command.Parameters.Add(new SqlParameter("@HallName", booking.HallName));
                command.Parameters.Add(new SqlParameter("@StartUtc", booking.StartUtc));
                command.Parameters.Add(new SqlParameter("@EndUtc", booking.EndUtc));
                command.Parameters.Add(new SqlParameter("@DurationHours", booking.DurationHours));
                command.Parameters.Add(new SqlParameter("@CustomerName", (object?)booking.CustomerName ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@HallRentalCost", booking.HallRentalCost));
                command.Parameters.Add(new SqlParameter("@ServicesCost", booking.ServicesCost));
                command.Parameters.Add(new SqlParameter("@TotalCost", booking.TotalCost));
                command.Parameters.Add(new SqlParameter("@IsCancelled", booking.IsCancelled));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", booking.CreatedAtUtc));

                await command.ExecuteNonQueryAsync(ct);
            }

            foreach (var service in booking.SelectedServices)
            {
                await using var command = _session.CreateCommand(connection);
                command.CommandText = $"""
                    INSERT INTO {SqlSchema.Table("BookingServiceItems")}
                        (Id, BookingId, Name, Price)
                    VALUES
                        (@Id, @BookingId, @Name, @Price)
                    """;
                command.Parameters.Add(new SqlParameter("@Id", service.Id));
                command.Parameters.Add(new SqlParameter("@BookingId", booking.Id));
                command.Parameters.Add(new SqlParameter("@Name", service.Name));
                command.Parameters.Add(new SqlParameter("@Price", service.Price));

                await command.ExecuteNonQueryAsync(ct);
            }
        }, IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await using var command = await _session.CreateCommandAsync(cancellationToken);
        command.CommandText = $"""
            UPDATE {SqlSchema.Table("Bookings")}
            SET IsCancelled = @IsCancelled
            WHERE Id = @Id
            """;
        command.Parameters.Add(new SqlParameter("@Id", booking.Id));
        command.Parameters.Add(new SqlParameter("@IsCancelled", booking.IsCancelled));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> HasOverlapAsync(
        Guid hallId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        await using var command = await _session.CreateCommandAsync(cancellationToken);
        command.CommandText = $"""
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM {SqlSchema.Table("Bookings")}
                WHERE HallId = @HallId
                  AND IsCancelled = 0
                  AND StartUtc < @End
                  AND EndUtc > @Start
            ) THEN 1 ELSE 0 END
            """;
        command.Parameters.Add(new SqlParameter("@HallId", hallId));
        command.Parameters.Add(new SqlParameter("@End", end));
        command.Parameters.Add(new SqlParameter("@Start", start));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    public async Task<BookingCountsRow> GetBookingCountsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        await using var command = await _session.CreateCommandAsync(cancellationToken);
        command.CommandText = $"""
            SELECT
                COUNT(*) AS TotalBookings,
                COALESCE(SUM(CASE WHEN IsCancelled = 0 THEN 1 ELSE 0 END), 0) AS ActiveBookings,
                COALESCE(SUM(CASE WHEN IsCancelled = 0 THEN TotalCost ELSE 0 END), 0) AS ActiveRevenue
            FROM {SqlSchema.Table("Bookings")}
            WHERE (@From IS NULL OR EndUtc > @From)
              AND (@To IS NULL OR StartUtc < @To)
            """;
        command.Parameters.Add(new SqlParameter("@From", (object?)from ?? DBNull.Value));
        command.Parameters.Add(new SqlParameter("@To", (object?)to ?? DBNull.Value));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new BookingCountsRow(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetDecimal(2));
    }

    public async Task<IReadOnlyList<HallRevenueRow>> GetRevenueByHallAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var byHallId = new Dictionary<Guid, (int BookingsCount, decimal TotalRevenue, decimal HallRentalRevenue, decimal ServicesRevenue)>();

        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT
                    HallId,
                    COUNT(*) AS BookingsCount,
                    SUM(TotalCost) AS TotalRevenue,
                    SUM(HallRentalCost) AS HallRentalRevenue,
                    SUM(ServicesCost) AS ServicesRevenue
                FROM {SqlSchema.Table("Bookings")}
                WHERE IsCancelled = 0
                  AND (@From IS NULL OR EndUtc > @From)
                  AND (@To IS NULL OR StartUtc < @To)
                GROUP BY HallId
                """;
            command.Parameters.Add(new SqlParameter("@From", (object?)from ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@To", (object?)to ?? DBNull.Value));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                byHallId[reader.GetGuid(0)] = (
                    reader.GetInt32(1),
                    reader.GetDecimal(2),
                    reader.GetDecimal(3),
                    reader.GetDecimal(4));
            }
        }

        var rows = new List<HallRevenueRow>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, Name
                FROM {SqlSchema.Table("Halls")}
                WHERE IsDeleted = 0
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var hallId = reader.GetGuid(0);
                var hallName = reader.GetString(1);

                if (!byHallId.TryGetValue(hallId, out var stats))
                {
                    rows.Add(new HallRevenueRow(hallId, hallName, 0, 0, 0, 0));
                    continue;
                }

                rows.Add(new HallRevenueRow(
                    hallId,
                    hallName,
                    stats.BookingsCount,
                    stats.TotalRevenue,
                    stats.HallRentalRevenue,
                    stats.ServicesRevenue));
            }
        }

        return rows.OrderByDescending(r => r.TotalRevenue).ToList();
    }

    public async Task<IReadOnlyList<HallOccupancyRow>> GetOccupancyByHallAsync(
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var hoursByHall = new Dictionary<Guid, (int Count, decimal Hours)>();

        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT
                    HallId,
                    COUNT(*) AS BookingsCount,
                    SUM(
                        CAST(DATEDIFF(SECOND,
                            CASE WHEN StartUtc > @RangeStart THEN StartUtc ELSE @RangeStart END,
                            CASE WHEN EndUtc < @RangeEnd THEN EndUtc ELSE @RangeEnd END
                        ) AS decimal(18, 6)) / 3600.0
                    ) AS BookedHours
                FROM {SqlSchema.Table("Bookings")}
                WHERE IsCancelled = 0
                  AND StartUtc < @RangeEnd
                  AND EndUtc > @RangeStart
                GROUP BY HallId
                """;
            command.Parameters.Add(new SqlParameter("@RangeStart", rangeStart));
            command.Parameters.Add(new SqlParameter("@RangeEnd", rangeEnd));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                hoursByHall[reader.GetGuid(0)] = (
                    reader.GetInt32(1),
                    reader.GetDecimal(2));
            }
        }

        var rows = new List<HallOccupancyRow>();
        await using (var command = _session.CreateCommand(connection))
        {
            command.CommandText = $"""
                SELECT Id, Name, Capacity
                FROM {SqlSchema.Table("Halls")}
                WHERE IsDeleted = 0
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var hallId = reader.GetGuid(0);
                hoursByHall.TryGetValue(hallId, out var stats);

                rows.Add(new HallOccupancyRow(
                    hallId,
                    reader.GetString(1),
                    reader.GetInt32(2),
                    stats.Count,
                    Math.Round(stats.Hours, 2, MidpointRounding.AwayFromZero)));
            }
        }

        return rows;
    }

    public async Task<IReadOnlyList<PopularServiceRow>> GetPopularServicesAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = new List<PopularServiceRow>();
        await using var command = _session.CreateCommand(connection);
        command.CommandText = $"""
            SELECT
                s.Name,
                COUNT(*) AS TimesBooked,
                SUM(s.Price) AS TotalRevenue
            FROM {SqlSchema.Table("BookingServiceItems")} AS s
            INNER JOIN {SqlSchema.Table("Bookings")} AS b ON b.Id = s.BookingId
            WHERE b.IsCancelled = 0
              AND (@From IS NULL OR b.EndUtc > @From)
              AND (@To IS NULL OR b.StartUtc < @To)
            GROUP BY s.Name
            ORDER BY COUNT(*) DESC
            """;
        command.Parameters.Add(new SqlParameter("@From", (object?)from ?? DBNull.Value));
        command.Parameters.Add(new SqlParameter("@To", (object?)to ?? DBNull.Value));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PopularServiceRow(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetDecimal(2)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<PeriodBookingRow>> GetBookingsGroupedByStartHourAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var rows = new List<PeriodBookingRow>();
        await using var command = _session.CreateCommand(connection);
        command.CommandText = $"""
            SELECT StartUtc, TotalCost
            FROM {SqlSchema.Table("Bookings")}
            WHERE IsCancelled = 0
              AND (@From IS NULL OR EndUtc > @From)
              AND (@To IS NULL OR StartUtc < @To)
            """;
        command.Parameters.Add(new SqlParameter("@From", (object?)from ?? DBNull.Value));
        command.Parameters.Add(new SqlParameter("@To", (object?)to ?? DBNull.Value));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PeriodBookingRow(
                reader.GetDateTime(0),
                reader.GetDecimal(1)));
        }

        return rows;
    }

    private static Booking ReadBooking(SqlDataReader reader) =>
        Booking.Restore(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetDateTime(3),
            reader.GetDateTime(4),
            reader.GetDecimal(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetDecimal(9),
            reader.GetBoolean(10),
            reader.GetDateTime(11));

    private static BookingServiceItem ReadBookingServiceItem(SqlDataReader reader) =>
        BookingServiceItem.Restore(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetDecimal(3));
}
