IF OBJECT_ID(N'[IGrinSchema].[Halls]', N'U') IS NULL
BEGIN
    CREATE TABLE [IGrinSchema].[Halls] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Capacity] int NOT NULL,
        [BaseHourlyRate] decimal(18, 2) NOT NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Halls] PRIMARY KEY ([Id])
    );

    CREATE INDEX [IX_Halls_Name] ON [IGrinSchema].[Halls] ([Name]);
END
GO

IF OBJECT_ID(N'[IGrinSchema].[HallServices]', N'U') IS NULL
BEGIN
    CREATE TABLE [IGrinSchema].[HallServices] (
        [Id] uniqueidentifier NOT NULL,
        [HallId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Price] decimal(18, 2) NOT NULL,
        CONSTRAINT [PK_HallServices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HallServices_Halls_HallId] FOREIGN KEY ([HallId])
            REFERENCES [IGrinSchema].[Halls] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[IGrinSchema].[Bookings]', N'U') IS NULL
BEGIN
    CREATE TABLE [IGrinSchema].[Bookings] (
        [Id] uniqueidentifier NOT NULL,
        [HallId] uniqueidentifier NOT NULL,
        [HallName] nvarchar(100) NOT NULL,
        [StartUtc] datetime2 NOT NULL,
        [EndUtc] datetime2 NOT NULL,
        [DurationHours] decimal(18, 2) NOT NULL,
        [CustomerName] nvarchar(200) NULL,
        [HallRentalCost] decimal(18, 2) NOT NULL,
        [ServicesCost] decimal(18, 2) NOT NULL,
        [TotalCost] decimal(18, 2) NOT NULL,
        [IsCancelled] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Bookings_Halls_HallId] FOREIGN KEY ([HallId])
            REFERENCES [IGrinSchema].[Halls] ([Id])
    );

    CREATE INDEX [IX_Bookings_HallId_StartUtc_EndUtc]
        ON [IGrinSchema].[Bookings] ([HallId], [StartUtc], [EndUtc]);
END
GO

IF OBJECT_ID(N'[IGrinSchema].[BookingServiceItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [IGrinSchema].[BookingServiceItems] (
        [Id] uniqueidentifier NOT NULL,
        [BookingId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Price] decimal(18, 2) NOT NULL,
        CONSTRAINT [PK_BookingServiceItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BookingServiceItems_Bookings_BookingId] FOREIGN KEY ([BookingId])
            REFERENCES [IGrinSchema].[Bookings] ([Id]) ON DELETE CASCADE
    );
END
GO
