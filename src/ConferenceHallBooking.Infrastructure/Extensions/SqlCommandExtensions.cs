using System.Data;
using ConferenceHallBooking.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ConferenceHallBooking.Infrastructure.Extensions;

public static class SqlCommandExtensions
{
    public static SqlCommand AsProcedure(this SqlCommand command, string procedureName)
    {
        command.CommandText = $"[{SqlSchema.Name}].[{procedureName}]";
        command.CommandType = CommandType.StoredProcedure;
        return command;
    }

    public static SqlCommand AddParam(this SqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    public static SqlCommand AddHallServiceTvpParam(
        this SqlCommand command,
        string name,
        IEnumerable<(Guid Id, string Name, decimal Price)> services)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Price", typeof(decimal));

        foreach (var service in services)
            table.Rows.Add(service.Id, service.Name, service.Price);

        var parameter = command.Parameters.Add(name, SqlDbType.Structured);
        parameter.TypeName = $"[{SqlSchema.Name}].[HallServiceListType]";
        parameter.Value = table;
        return command;
    }
}
