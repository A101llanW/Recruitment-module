IF COL_LENGTH(N'dbo.Positions', N'ExpiryDate') IS NULL
BEGIN
    ALTER TABLE dbo.Positions
        ADD ExpiryDate DATETIME NULL;
END;
GO
