using ConferenceHallBooking.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ConferenceHallBooking.Infrastructure.Repositories;

public abstract class AdoRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    protected AdoRepository(ISqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    protected async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
