CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Bookings_GetByDateRange]
    @From datetime2,
    @To datetime2
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, HallId, HallName, StartUtc, EndUtc, DurationHours, CustomerName,
           HallRentalCost, ServicesCost, TotalCost, IsCancelled, CreatedAtUtc
    FROM [IGrinSchema].[Bookings]
    WHERE IsCancelled = 0
      AND StartUtc < @To
      AND EndUtc > @From
    ORDER BY CreatedAtUtc DESC;

    SELECT s.Id, s.BookingId, s.Name, s.Price
    FROM [IGrinSchema].[BookingServiceItems] AS s
    INNER JOIN [IGrinSchema].[Bookings] AS b ON b.Id = s.BookingId
    WHERE b.IsCancelled = 0
      AND b.StartUtc < @To
      AND b.EndUtc > @From
    ORDER BY s.Name;
END
GO
