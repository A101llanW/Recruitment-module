-- Add LogoPath to Companies (idempotent)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Companies')
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Companies')
          AND name = 'LogoPath'
    )
    BEGIN
        ALTER TABLE dbo.Companies ADD LogoPath NVARCHAR(260) NULL;
        PRINT 'Added Companies.LogoPath';
    END
END
