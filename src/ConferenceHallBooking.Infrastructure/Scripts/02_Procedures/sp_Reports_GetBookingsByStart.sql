CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Reports_GetBookingsByStart]
    @From datetime2 = NULL,
    @To datetime2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT StartUtc, TotalCost
    FROM [IGrinSchema].[Bookings]
    WHERE IsCancelled = 0
      AND (@From IS NULL OR EndUtc > @From)
      AND (@To IS NULL OR StartUtc < @To);
END
GO
