CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Halls_Update]
    @Id uniqueidentifier,
    @Name nvarchar(100),
    @Capacity int,
    @BaseHourlyRate decimal(18, 2),
    @IsDeleted bit,
    @UpdatedAtUtc datetime2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [IGrinSchema].[Halls]
    SET Name = @Name,
        Capacity = @Capacity,
        BaseHourlyRate = @BaseHourlyRate,
        IsDeleted = @IsDeleted,
        UpdatedAtUtc = @UpdatedAtUtc
    WHERE Id = @Id;
END
GO
