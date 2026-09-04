using ConferenceHallBooking.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConferenceHallBooking.Infrastructure.Health;

public sealed class SqlConnectionHealthCheck : IHealthCheck
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlConnectionHealthCheck(ISqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server is unavailable.", ex);
        }
    }
}
