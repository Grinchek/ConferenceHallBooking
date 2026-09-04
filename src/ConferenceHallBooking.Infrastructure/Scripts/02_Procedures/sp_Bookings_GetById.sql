CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Bookings_GetById]
    @Id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, HallId, HallName, StartUtc, EndUtc, DurationHours, CustomerName,
           HallRentalCost, ServicesCost, TotalCost, IsCancelled, CreatedAtUtc
    FROM [IGrinSchema].[Bookings]
    WHERE Id = @Id;

    SELECT Id, BookingId, Name, Price
    FROM [IGrinSchema].[BookingServiceItems]
    WHERE BookingId = @Id
    ORDER BY Name;
END
GO
