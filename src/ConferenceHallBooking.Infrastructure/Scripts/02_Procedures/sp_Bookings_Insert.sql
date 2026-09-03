CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Bookings_Insert]
    @Id uniqueidentifier,
    @HallId uniqueidentifier,
    @HallName nvarchar(100),
    @StartUtc datetime2,
    @EndUtc datetime2,
    @DurationHours decimal(18, 2),
    @CustomerName nvarchar(200) = NULL,
    @HallRentalCost decimal(18, 2),
    @ServicesCost decimal(18, 2),
    @TotalCost decimal(18, 2),
    @IsCancelled bit,
    @CreatedAtUtc datetime2,
    @Services [IGrinSchema].[HallServiceListType] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1
        FROM [IGrinSchema].[Bookings] WITH (UPDLOCK, HOLDLOCK)
        WHERE HallId = @HallId
          AND IsCancelled = 0
          AND StartUtc < @EndUtc
          AND EndUtc > @StartUtc
    )
    BEGIN
        THROW 50001, N'Hall is already booked for the requested time range.', 1;
    END

    INSERT INTO [IGrinSchema].[Bookings]
        (Id, HallId, HallName, StartUtc, EndUtc, DurationHours, CustomerName,
         HallRentalCost, ServicesCost, TotalCost, IsCancelled, CreatedAtUtc)
    VALUES
        (@Id, @HallId, @HallName, @StartUtc, @EndUtc, @DurationHours, @CustomerName,
         @HallRentalCost, @ServicesCost, @TotalCost, @IsCancelled, @CreatedAtUtc);

    INSERT INTO [IGrinSchema].[BookingServiceItems] (Id, BookingId, Name, Price)
    SELECT Id, @Id, Name, Price
    FROM @Services;

    COMMIT TRANSACTION;
END
GO
