IF OBJECT_ID(N'[IGrinSchema].[Bookings]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[IGrinSchema].[Bookings]', N'HallName') IS NULL
BEGIN
    ALTER TABLE [IGrinSchema].[Bookings]
    ADD [HallName] nvarchar(100) NOT NULL
        CONSTRAINT [DF_Bookings_HallName] DEFAULT (N'');
END
GO

IF OBJECT_ID(N'[IGrinSchema].[Bookings]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[IGrinSchema].[Bookings]', N'HallName') IS NOT NULL
BEGIN
    UPDATE b
    SET b.[HallName] = COALESCE(NULLIF(h.[Name], N''), N'Unknown')
    FROM [IGrinSchema].[Bookings] b
    LEFT JOIN [IGrinSchema].[Halls] h ON h.[Id] = b.[HallId]
    WHERE b.[HallName] = N'' OR b.[HallName] IS NULL;
END
GO
