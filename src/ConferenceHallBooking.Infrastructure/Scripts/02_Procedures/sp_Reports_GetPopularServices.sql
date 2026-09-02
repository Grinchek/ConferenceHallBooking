CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Reports_GetPopularServices]
    @From datetime2 = NULL,
    @To datetime2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.Name,
        CAST(COUNT(*) AS int) AS TimesBooked,
        SUM(s.Price) AS TotalRevenue
    FROM [IGrinSchema].[BookingServiceItems] AS s
    INNER JOIN [IGrinSchema].[Bookings] AS b ON b.Id = s.BookingId
    WHERE b.IsCancelled = 0
      AND (@From IS NULL OR b.EndUtc > @From)
      AND (@To IS NULL OR b.StartUtc < @To)
    GROUP BY s.Name
    ORDER BY COUNT(*) DESC;
END
GO
