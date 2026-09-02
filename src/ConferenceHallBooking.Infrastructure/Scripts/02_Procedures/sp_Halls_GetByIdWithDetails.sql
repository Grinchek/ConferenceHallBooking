CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Halls_GetByIdWithDetails]
    @Id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc
    FROM [IGrinSchema].[Halls]
    WHERE Id = @Id AND IsDeleted = 0;

    SELECT Id, HallId, Name, Price
    FROM [IGrinSchema].[HallServices]
    WHERE HallId = @Id
    ORDER BY Name;
END
GO
