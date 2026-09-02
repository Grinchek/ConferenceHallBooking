using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ConferenceHallBooking.Infrastructure.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. Set it in appsettings or user secrets.");
    }

    public SqlConnection Create() => new(_connectionString);
}
