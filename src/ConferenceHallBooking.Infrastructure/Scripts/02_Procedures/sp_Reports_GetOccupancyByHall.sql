CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Reports_GetOccupancyByHall]
    @RangeStart datetime2,
    @RangeEnd datetime2
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        h.Id AS HallId,
        h.Name AS HallName,
        h.Capacity,
        COALESCE(CAST(agg.BookingsCount AS int), 0) AS BookingsCount,
        COALESCE(agg.BookedHours, 0) AS BookedHours
    FROM [IGrinSchema].[Halls] AS h
    LEFT JOIN (
        SELECT
            HallId,
            COUNT(*) AS BookingsCount,
            SUM(
                CAST(DATEDIFF(SECOND,
                    CASE WHEN StartUtc > @RangeStart THEN StartUtc ELSE @RangeStart END,
                    CASE WHEN EndUtc < @RangeEnd THEN EndUtc ELSE @RangeEnd END
                ) AS decimal(18, 6)) / 3600.0
            ) AS BookedHours
        FROM [IGrinSchema].[Bookings]
        WHERE IsCancelled = 0
          AND StartUtc < @RangeEnd
          AND EndUtc > @RangeStart
        GROUP BY HallId
    ) AS agg ON agg.HallId = h.Id
    WHERE h.IsDeleted = 0;
END
GO
