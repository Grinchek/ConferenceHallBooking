IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'IGrinSchema')
    EXEC(N'CREATE SCHEMA [IGrinSchema]');
