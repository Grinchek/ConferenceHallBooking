using System.Data;
using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Infrastructure.Data;
using ConferenceHallBooking.Infrastructure.Extensions;
using Microsoft.Data.SqlClient;

namespace ConferenceHallBooking.Infrastructure.Repositories;

public sealed class HallRepository : IHallRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public HallRepository(ISqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<Hall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Halls.GetById)
            .AddParam("@Id", SqlDbType.UniqueIdentifier, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadHall(reader);
    }

    public async Task<Hall?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Halls.GetByIdWithDetails)
            .AddParam("@Id", SqlDbType.UniqueIdentifier, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var hall = ReadHall(reader);
        var services = new List<HallService>();

        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                services.Add(ReadHallService(reader));
        }

        hall.RestoreServices(services);
        return hall;
    }

    public async Task<IReadOnlyList<Hall>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.AsProcedure(SqlProcedures.Halls.GetAll);

        return await ReadHallsWithServicesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<Hall>> SearchAvailableAsync(
        DateTime start,
        DateTime end,
        int requiredCapacity,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Halls.SearchAvailable)
            .AddParam("@Start", SqlDbType.DateTime2, start)
            .AddParam("@End", SqlDbType.DateTime2, end)
            .AddParam("@RequiredCapacity", SqlDbType.Int, requiredCapacity);

        return await ReadHallsWithServicesAsync(command, cancellationToken);
    }

    public async Task AddAsync(Hall hall, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Halls.Insert)
            .AddParam("@Id", SqlDbType.UniqueIdentifier, hall.Id)
            .AddParam("@Name", SqlDbType.NVarChar, hall.Name, 100)
            .AddParam("@Capacity", SqlDbType.Int, hall.Capacity)
            .AddParam("@BaseHourlyRate", SqlDbType.Decimal, 18, 2, hall.BaseHourlyRate)
            .AddParam("@IsDeleted", SqlDbType.Bit, hall.IsDeleted)
            .AddParam("@CreatedAtUtc", SqlDbType.DateTime2, hall.CreatedAtUtc)
            .AddParam("@UpdatedAtUtc", SqlDbType.DateTime2, hall.UpdatedAtUtc)
            .AddHallServiceTvpParam(
                "@Services",
                hall.Services.Select(s => (s.Id, s.Name, s.Price)));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Hall hall, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Halls.Update)
            .AddParam("@Id", SqlDbType.UniqueIdentifier, hall.Id)
            .AddParam("@Name", SqlDbType.NVarChar, hall.Name, 100)
            .AddParam("@Capacity", SqlDbType.Int, hall.Capacity)
            .AddParam("@BaseHourlyRate", SqlDbType.Decimal, 18, 2, hall.BaseHourlyRate)
            .AddParam("@IsDeleted", SqlDbType.Bit, hall.IsDeleted)
            .AddParam("@UpdatedAtUtc", SqlDbType.DateTime2, hall.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetServicesAsync(
        Guid hallId,
        IEnumerable<(string Name, decimal Price)> services,
        CancellationToken cancellationToken = default)
    {
        var rows = services
            .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(s =>
            {
                var entity = new HallService(s.Name, s.Price, hallId);
                return (entity.Id, entity.Name, entity.Price);
            })
            .ToList();

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Halls.SetServices)
            .AddParam("@HallId", SqlDbType.UniqueIdentifier, hallId)
            .AddHallServiceTvpParam("@Services", rows);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command
            .AsProcedure(SqlProcedures.Halls.ExistsByName)
            .AddParam("@Name", SqlDbType.NVarChar, name.Trim(), 100)
            .AddParam("@ExcludeId", SqlDbType.UniqueIdentifier, excludeId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<IReadOnlyList<Hall>> ReadHallsWithServicesAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var halls = new List<Hall>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            halls.Add(ReadHall(reader));

        if (halls.Count == 0 || !await reader.NextResultAsync(cancellationToken))
            return halls;

        var servicesByHallId = new Dictionary<Guid, List<HallService>>();
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
}
