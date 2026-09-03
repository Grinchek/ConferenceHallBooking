CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Halls_SetServices]
    @HallId uniqueidentifier,
    @Services [IGrinSchema].[HallServiceListType] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE FROM [IGrinSchema].[HallServices]
    WHERE HallId = @HallId;

    INSERT INTO [IGrinSchema].[HallServices] (Id, HallId, Name, Price)
    SELECT Id, @HallId, Name, Price
    FROM @Services;

    COMMIT TRANSACTION;
END
GO
