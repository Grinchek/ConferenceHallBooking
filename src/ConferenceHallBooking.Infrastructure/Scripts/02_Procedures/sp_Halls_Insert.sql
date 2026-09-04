CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Halls_Insert]
    @Id uniqueidentifier,
    @Name nvarchar(100),
    @Capacity int,
    @BaseHourlyRate decimal(18, 2),
    @IsDeleted bit,
    @CreatedAtUtc datetime2,
    @UpdatedAtUtc datetime2 = NULL,
    @Services [IGrinSchema].[HallServiceListType] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    INSERT INTO [IGrinSchema].[Halls]
        (Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
    VALUES
        (@Id, @Name, @Capacity, @BaseHourlyRate, @IsDeleted, @CreatedAtUtc, @UpdatedAtUtc);

    INSERT INTO [IGrinSchema].[HallServices] (Id, HallId, Name, Price)
    SELECT Id, @Id, Name, Price
    FROM @Services;

    COMMIT TRANSACTION;
END
GO
