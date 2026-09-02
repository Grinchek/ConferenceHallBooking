CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Bookings_HasOverlap]
    @HallId uniqueidentifier,
    @Start datetime2,
    @End datetime2
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM [IGrinSchema].[Bookings] WITH (UPDLOCK, HOLDLOCK)
        WHERE HallId = @HallId
          AND IsCancelled = 0
          AND StartUtc < @End
          AND EndUtc > @Start
    ) THEN 1 ELSE 0 END;
END
GO
