namespace ConferenceHallBooking.Infrastructure.Data;

public static class SqlSchema
{
    public const string Name = "IGrinSchema";

    public static string Table(string tableName) => $"[{Name}].[{tableName}]";
}
