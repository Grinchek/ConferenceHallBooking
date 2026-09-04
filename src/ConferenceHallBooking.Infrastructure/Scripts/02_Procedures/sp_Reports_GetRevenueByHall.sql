CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Reports_GetRevenueByHall]
    @From datetime2 = NULL,
    @To datetime2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        h.Id AS HallId,
        h.Name AS HallName,
        COALESCE(CAST(agg.BookingsCount AS int), 0) AS BookingsCount,
        COALESCE(agg.TotalRevenue, 0) AS TotalRevenue,
        COALESCE(agg.HallRentalRevenue, 0) AS HallRentalRevenue,
        COALESCE(agg.ServicesRevenue, 0) AS ServicesRevenue
    FROM [IGrinSchema].[Halls] AS h
    LEFT JOIN (
        SELECT
            HallId,
            COUNT(*) AS BookingsCount,
            SUM(TotalCost) AS TotalRevenue,
            SUM(HallRentalCost) AS HallRentalRevenue,
            SUM(ServicesCost) AS ServicesRevenue
        FROM [IGrinSchema].[Bookings]
        WHERE IsCancelled = 0
          AND (@From IS NULL OR EndUtc > @From)
          AND (@To IS NULL OR StartUtc < @To)
        GROUP BY HallId
    ) AS agg ON agg.HallId = h.Id
    WHERE h.IsDeleted = 0
    ORDER BY COALESCE(agg.TotalRevenue, 0) DESC;
END
GO
