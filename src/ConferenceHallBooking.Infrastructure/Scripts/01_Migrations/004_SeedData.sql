IF NOT EXISTS (SELECT 1 FROM [IGrinSchema].[Halls])
BEGIN
    DECLARE @HallA uniqueidentifier = 'AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA';
    DECLARE @HallB uniqueidentifier = 'BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB';
    DECLARE @HallC uniqueidentifier = 'CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC';
    DECLARE @Now datetime2 = SYSUTCDATETIME();

    INSERT INTO [IGrinSchema].[Halls]
        ([Id], [Name], [Capacity], [BaseHourlyRate], [IsDeleted], [CreatedAtUtc], [UpdatedAtUtc])
    VALUES
        (@HallA, N'Зал А', 50, 2000.00, 0, @Now, NULL),
        (@HallB, N'Зал B', 100, 3500.00, 0, @Now, NULL),
        (@HallC, N'Зал C', 30, 1500.00, 0, @Now, NULL);

    INSERT INTO [IGrinSchema].[HallServices] ([Id], [HallId], [Name], [Price])
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
END
GO
