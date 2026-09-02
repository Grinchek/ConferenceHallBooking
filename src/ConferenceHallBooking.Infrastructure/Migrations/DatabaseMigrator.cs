using System.Reflection;
using ConferenceHallBooking.Infrastructure.Data;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;

namespace ConferenceHallBooking.Infrastructure.Migrations;

public static class DatabaseMigrator
{
    public static void Migrate(string connectionString)
    {
        EnsureSchemaExists(connectionString);

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .JournalToSqlTable(SqlSchema.Name, "SchemaVersions")
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        DatabaseUpgradeResult result = upgrader.PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException($"Database migration failed: {result.Error.Message}", result.Error);
    }

    private static void EnsureSchemaExists(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{SqlSchema.Name}')
                EXEC(N'CREATE SCHEMA [{SqlSchema.Name}]');
            """;
        command.ExecuteNonQuery();
    }
}
