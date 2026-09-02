CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Halls_SearchAvailable]
    @Start datetime2,
    @End datetime2,
    @RequiredCapacity int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT h.Id, h.Name, h.Capacity, h.BaseHourlyRate, h.IsDeleted, h.CreatedAtUtc, h.UpdatedAtUtc
    FROM [IGrinSchema].[Halls] AS h
    WHERE h.IsDeleted = 0
      AND h.Capacity >= @RequiredCapacity
      AND NOT EXISTS (
          SELECT 1
          FROM [IGrinSchema].[Bookings] AS b
          WHERE b.HallId = h.Id
            AND b.IsCancelled = 0
            AND b.StartUtc < @End
            AND b.EndUtc > @Start
      )
    ORDER BY h.BaseHourlyRate;

    SELECT s.Id, s.HallId, s.Name, s.Price
    FROM [IGrinSchema].[HallServices] AS s
    INNER JOIN [IGrinSchema].[Halls] AS h ON h.Id = s.HallId
    WHERE h.IsDeleted = 0
      AND h.Capacity >= @RequiredCapacity
      AND NOT EXISTS (
          SELECT 1
          FROM [IGrinSchema].[Bookings] AS b
          WHERE b.HallId = h.Id
            AND b.IsCancelled = 0
            AND b.StartUtc < @End
            AND b.EndUtc > @Start
      )
    ORDER BY s.Name;
END
GO
