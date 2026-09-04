using System.Reflection;
using ConferenceHallBooking.Infrastructure.Data;
using DbUp;
using DbUp.Engine;
using DbUp.Helpers;
using Microsoft.Data.SqlClient;

namespace ConferenceHallBooking.Infrastructure.Migrations;

public static class DatabaseMigrator
{
    private static readonly Assembly ScriptsAssembly = Assembly.GetExecutingAssembly();

    public static void Migrate(string connectionString)
    {
        EnsureSchemaExists(connectionString);

        // Tables/types/seed: run once, tracked in SchemaVersions.
        EnsureSuccessful(
            DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(ScriptsAssembly, IsMigrationScript)
                .JournalToSqlTable(SqlSchema.Name, "SchemaVersions")
                .WithTransactionPerScript()
                .LogToConsole()
                .Build()
                .PerformUpgrade(),
            "Database migration");

        // Stored procedures: CREATE OR ALTER is idempotent, re-apply on every startup.
        // NullJournal avoids permanently skipping an updated .sql with the same file name.
        EnsureSuccessful(
            DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(ScriptsAssembly, IsProcedureScript)
                .JournalTo(new NullJournal())
                .WithTransactionPerScript()
                .LogToConsole()
                .Build()
                .PerformUpgrade(),
            "Stored procedure deployment");
    }

    // Folder names like 01_Migrations become _01_Migrations in embedded resource names.
    private static bool IsMigrationScript(string scriptName) =>
        scriptName.Contains(".Scripts._01_Migrations.", StringComparison.Ordinal);

    private static bool IsProcedureScript(string scriptName) =>
        scriptName.Contains(".Scripts._02_Procedures.", StringComparison.Ordinal);

    private static void EnsureSuccessful(DatabaseUpgradeResult result, string stepName)
    {
        if (!result.Successful)
            throw new InvalidOperationException($"{stepName} failed: {result.Error.Message}", result.Error);
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
