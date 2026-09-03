using Microsoft.Data.SqlClient;

namespace ConferenceHallBooking.Infrastructure.Data;

public interface ISqlConnectionFactory
{
    SqlConnection Create();
}
