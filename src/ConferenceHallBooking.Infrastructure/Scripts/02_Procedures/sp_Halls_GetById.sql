CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Halls_GetById]
    @Id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc
    FROM [IGrinSchema].[Halls]
    WHERE Id = @Id AND IsDeleted = 0;
END
GO
