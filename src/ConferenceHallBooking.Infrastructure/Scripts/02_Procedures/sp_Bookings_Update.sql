CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Bookings_Update]
    @Id uniqueidentifier,
    @IsCancelled bit
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [IGrinSchema].[Bookings]
    SET IsCancelled = @IsCancelled
    WHERE Id = @Id;
END
GO
