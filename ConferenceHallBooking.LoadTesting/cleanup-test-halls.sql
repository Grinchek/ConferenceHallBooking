-- Cleanup load-test halls in IGrinSchema (safe for shared Azure SQL).
-- Keeps/restores seed halls A/B/C; removes LoadDev-% and LoadPut-%.

SET NOCOUNT ON;

PRINT '=== BEFORE ===';
SELECT
    COUNT(*) AS TotalHalls,
    SUM(CASE WHEN IsDeleted = 0 THEN 1 ELSE 0 END) AS ActiveHalls,
    SUM(CASE WHEN Name LIKE N'LoadDev-%' OR Name LIKE N'LoadPut-%' THEN 1 ELSE 0 END) AS TestHalls
FROM [IGrinSchema].[Halls];

BEGIN TRAN;

DECLARE @toDelete TABLE (Id uniqueidentifier PRIMARY KEY);

INSERT INTO @toDelete (Id)
SELECT Id
FROM [IGrinSchema].[Halls]
WHERE Name LIKE N'LoadDev-%'
   OR Name LIKE N'LoadPut-%';

DECLARE @hallCount int = (SELECT COUNT(*) FROM @toDelete);
PRINT CONCAT('Halls to delete: ', @hallCount);

-- Bookings for those halls (FK has no CASCADE)
DELETE bsi
FROM [IGrinSchema].[BookingServiceItems] bsi
INNER JOIN [IGrinSchema].[Bookings] b ON b.Id = bsi.BookingId
INNER JOIN @toDelete d ON d.Id = b.HallId;

DELETE b
FROM [IGrinSchema].[Bookings] b
INNER JOIN @toDelete d ON d.Id = b.HallId;

-- Halls (HallServices CASCADE)
DELETE h
FROM [IGrinSchema].[Halls] h
INNER JOIN @toDelete d ON d.Id = h.Id;

-- Restore seed halls A/B/C if missing or soft-deleted / renamed away
DECLARE @HallA uniqueidentifier = 'AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA';
DECLARE @HallB uniqueidentifier = 'BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB';
DECLARE @HallC uniqueidentifier = 'CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC';
DECLARE @Now datetime2 = SYSUTCDATETIME();

MERGE [IGrinSchema].[Halls] AS t
USING (VALUES
    (@HallA, N'Зал А', 50, 2000.00),
    (@HallB, N'Зал B', 100, 3500.00),
    (@HallC, N'Зал C', 30, 1500.00)
) AS s (Id, Name, Capacity, BaseHourlyRate)
ON t.Id = s.Id
WHEN MATCHED THEN
    UPDATE SET
        Name = s.Name,
        Capacity = s.Capacity,
        BaseHourlyRate = s.BaseHourlyRate,
        IsDeleted = 0,
        UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Capacity, BaseHourlyRate, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
    VALUES (s.Id, s.Name, s.Capacity, s.BaseHourlyRate, 0, @Now, NULL);

-- Refresh default services for seed halls
DELETE FROM [IGrinSchema].[HallServices]
WHERE HallId IN (@HallA, @HallB, @HallC);

INSERT INTO [IGrinSchema].[HallServices] (Id, HallId, Name, Price)
VALUES
    (NEWID(), @HallA, N'Проєктор', 500.00),
    (NEWID(), @HallA, N'Wi-Fi', 300.00),
    (NEWID(), @HallA, N'Звук', 700.00),
    (NEWID(), @HallB, N'Проєктор', 500.00),
    (NEWID(), @HallB, N'Wi-Fi', 300.00),
    (NEWID(), @HallB, N'Звук', 700.00),
    (NEWID(), @HallC, N'Проєктор', 500.00),
    (NEWID(), @HallC, N'Wi-Fi', 300.00),
    (NEWID(), @HallC, N'Звук', 700.00);

PRINT '=== AFTER (before commit) ===';
SELECT
    COUNT(*) AS TotalHalls,
    SUM(CASE WHEN IsDeleted = 0 THEN 1 ELSE 0 END) AS ActiveHalls,
    SUM(CASE WHEN Name LIKE N'LoadDev-%' OR Name LIKE N'LoadPut-%' THEN 1 ELSE 0 END) AS TestHalls
FROM [IGrinSchema].[Halls];

SELECT Id, Name, Capacity, BaseHourlyRate, IsDeleted
FROM [IGrinSchema].[Halls]
WHERE IsDeleted = 0
ORDER BY Name;

COMMIT;
PRINT 'Committed.';
