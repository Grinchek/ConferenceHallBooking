CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Reports_GetBookingCounts]
    @From datetime2 = NULL,
    @To datetime2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CAST(COUNT(*) AS int) AS TotalBookings,
        CAST(COALESCE(SUM(CASE WHEN IsCancelled = 0 THEN 1 ELSE 0 END), 0) AS int) AS ActiveBookings,
        COALESCE(SUM(CASE WHEN IsCancelled = 0 THEN TotalCost ELSE 0 END), 0) AS ActiveRevenue
    FROM [IGrinSchema].[Bookings]
    WHERE (@From IS NULL OR EndUtc > @From)
      AND (@To IS NULL OR StartUtc < @To);
END
GO
