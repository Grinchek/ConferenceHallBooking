namespace ConferenceHallBooking.Infrastructure.Data;

public static class SqlProcedures
{
    public static class Halls
    {
        public const string ExistsByName = "sp_Halls_ExistsByName";
        public const string GetById = "sp_Halls_GetById";
        public const string GetByIdWithDetails = "sp_Halls_GetByIdWithDetails";
        public const string GetAll = "sp_Halls_GetAll";
        public const string SearchAvailable = "sp_Halls_SearchAvailable";
        public const string Insert = "sp_Halls_Insert";
        public const string Update = "sp_Halls_Update";
        public const string SetServices = "sp_Halls_SetServices";
    }

    public static class Bookings
    {
        public const string GetById = "sp_Bookings_GetById";
        public const string GetAll = "sp_Bookings_GetAll";
        public const string GetByDateRange = "sp_Bookings_GetByDateRange";
        public const string Insert = "sp_Bookings_Insert";
        public const string Update = "sp_Bookings_Update";
    }

    public static class Reports
    {
        public const string GetBookingCounts = "sp_Reports_GetBookingCounts";
        public const string GetRevenueByHall = "sp_Reports_GetRevenueByHall";
        public const string GetOccupancyByHall = "sp_Reports_GetOccupancyByHall";
        public const string GetPopularServices = "sp_Reports_GetPopularServices";
        public const string GetBookingsByStart = "sp_Reports_GetBookingsByStart";
    }
}
