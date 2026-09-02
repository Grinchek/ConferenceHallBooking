CREATE OR ALTER PROCEDURE [IGrinSchema].[sp_Halls_ExistsByName]
    @Name nvarchar(100),
    @ExcludeId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM [IGrinSchema].[Halls]
        WHERE IsDeleted = 0
          AND LOWER(Name) = LOWER(@Name)
          AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
    ) THEN 1 ELSE 0 END;
END
GO
