IF TYPE_ID(N'[IGrinSchema].[HallServiceListType]') IS NULL
BEGIN
    CREATE TYPE [IGrinSchema].[HallServiceListType] AS TABLE
    (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Price] decimal(18, 2) NOT NULL
    );
END
GO
