using System.Data;
using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Domain.Exceptions;
using ConferenceHallBooking.Infrastructure.Data;
using ConferenceHallBooking.Infrastructure.Extensions;
using Microsoft.Data.SqlClient;

namespace ConferenceHallBooking.Infrastructure.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public BookingRepository(ISqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Bookings.GetById)
            .AddParam("@Id", SqlDbType.UniqueIdentifier, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var booking = ReadBooking(reader);
        var services = new List<BookingServiceItem>();

        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                services.Add(ReadBookingServiceItem(reader));
        }

        booking.RestoreSelectedServices(services);
        return booking;
    }

    public async Task<IReadOnlyList<Booking>> GetAllAsync(
        bool includeCancelled = false,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Bookings.GetAll)
            .AddParam("@IncludeCancelled", SqlDbType.Bit, includeCancelled);

        return await ReadBookingsWithServicesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Bookings.GetByDateRange)
            .AddParam("@From", SqlDbType.DateTime2, from)
            .AddParam("@To", SqlDbType.DateTime2, to);

        return await ReadBookingsWithServicesAsync(command, cancellationToken);
    }

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Bookings.Insert)
            .AddParam("@Id", SqlDbType.UniqueIdentifier, booking.Id)
            .AddParam("@HallId", SqlDbType.UniqueIdentifier, booking.HallId)
            .AddParam("@HallName", SqlDbType.NVarChar, booking.HallName, 100)
            .AddParam("@StartUtc", SqlDbType.DateTime2, booking.StartUtc)
            .AddParam("@EndUtc", SqlDbType.DateTime2, booking.EndUtc)
            .AddParam("@DurationHours", SqlDbType.Decimal, 18, 2, booking.DurationHours)
            .AddParam("@CustomerName", SqlDbType.NVarChar, booking.CustomerName, 200)
            .AddParam("@HallRentalCost", SqlDbType.Decimal, 18, 2, booking.HallRentalCost)
            .AddParam("@ServicesCost", SqlDbType.Decimal, 18, 2, booking.ServicesCost)
            .AddParam("@TotalCost", SqlDbType.Decimal, 18, 2, booking.TotalCost)
            .AddParam("@IsCancelled", SqlDbType.Bit, booking.IsCancelled)
            .AddParam("@CreatedAtUtc", SqlDbType.DateTime2, booking.CreatedAtUtc)
            .AddHallServiceTvpParam(
                "@Services",
                booking.SelectedServices.Select(s => (s.Id, s.Name, s.Price)));

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            throw new ConflictException(
                $"Зал '{booking.HallName}' уже заброньовано на період {booking.StartUtc:g} – {booking.EndUtc:g}.");
        }
    }

    public async Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Bookings.Update)
            .AddParam("@Id", SqlDbType.UniqueIdentifier, booking.Id)
            .AddParam("@IsCancelled", SqlDbType.Bit, booking.IsCancelled);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BookingCountsRow> GetBookingCountsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Reports.GetBookingCounts)
            .AddParam("@From", SqlDbType.DateTime2, from)
            .AddParam("@To", SqlDbType.DateTime2, to);

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
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Reports.GetRevenueByHall)
            .AddParam("@From", SqlDbType.DateTime2, from)
            .AddParam("@To", SqlDbType.DateTime2, to);

        var rows = new List<HallRevenueRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new HallRevenueRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<HallOccupancyRow>> GetOccupancyByHallAsync(
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Reports.GetOccupancyByHall)
            .AddParam("@RangeStart", SqlDbType.DateTime2, rangeStart)
            .AddParam("@RangeEnd", SqlDbType.DateTime2, rangeEnd);

        var rows = new List<HallOccupancyRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new HallOccupancyRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                Math.Round(reader.GetDecimal(4), 2, MidpointRounding.AwayFromZero)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<PopularServiceRow>> GetPopularServicesAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Reports.GetPopularServices)
            .AddParam("@From", SqlDbType.DateTime2, from)
            .AddParam("@To", SqlDbType.DateTime2, to);

        var rows = new List<PopularServiceRow>();
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
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Reports.GetBookingsByStart)
            .AddParam("@From", SqlDbType.DateTime2, from)
            .AddParam("@To", SqlDbType.DateTime2, to);

        var rows = new List<PeriodBookingRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PeriodBookingRow(
                reader.GetDateTime(0),
                reader.GetDecimal(1)));
        }

        return rows;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<IReadOnlyList<Booking>> ReadBookingsWithServicesAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var bookings = new List<Booking>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            bookings.Add(ReadBooking(reader));

        if (bookings.Count == 0 || !await reader.NextResultAsync(cancellationToken))
            return bookings;

        var servicesByBookingId = new Dictionary<Guid, List<BookingServiceItem>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var service = ReadBookingServiceItem(reader);
            if (!servicesByBookingId.TryGetValue(service.BookingId, out var list))
            {
                list = [];
                servicesByBookingId[service.BookingId] = list;
            }

            list.Add(service);
        }

        foreach (var booking in bookings)
        {
            if (servicesByBookingId.TryGetValue(booking.Id, out var services))
                booking.RestoreSelectedServices(services);
        }

        return bookings;
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
