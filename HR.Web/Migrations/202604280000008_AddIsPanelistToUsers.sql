-- Add IsPanelist to Users (idempotent)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users')
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Users')
          AND name = 'IsPanelist'
    )
    BEGIN
        ALTER TABLE dbo.Users
            ADD IsPanelist BIT NOT NULL CONSTRAINT DF_Users_IsPanelist DEFAULT(0);
        PRINT 'Added Users.IsPanelist';
    END
END
GO
