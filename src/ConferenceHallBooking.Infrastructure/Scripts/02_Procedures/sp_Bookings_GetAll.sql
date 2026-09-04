CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Bookings_GetAll]
    @IncludeCancelled bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, HallId, HallName, StartUtc, EndUtc, DurationHours, CustomerName,
           HallRentalCost, ServicesCost, TotalCost, IsCancelled, CreatedAtUtc
    FROM [IGrinSchema].[Bookings]
    WHERE @IncludeCancelled = 1 OR IsCancelled = 0
    ORDER BY CreatedAtUtc DESC;

    SELECT s.Id, s.BookingId, s.Name, s.Price
    FROM [IGrinSchema].[BookingServiceItems] AS s
    INNER JOIN [IGrinSchema].[Bookings] AS b ON b.Id = s.BookingId
    WHERE @IncludeCancelled = 1 OR b.IsCancelled = 0
    ORDER BY s.Name;
END
GO
