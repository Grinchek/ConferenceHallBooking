CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Halls_GetAll]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc
    FROM [IGrinSchema].[Halls]
    WHERE IsDeleted = 0
    ORDER BY Name;

    SELECT s.Id, s.HallId, s.Name, s.Price
    FROM [IGrinSchema].[HallServices] AS s
    INNER JOIN [IGrinSchema].[Halls] AS h ON h.Id = s.HallId
    WHERE h.IsDeleted = 0
    ORDER BY s.Name;
END
GO
