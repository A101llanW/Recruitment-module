-- Add CoverLetter to Applications (idempotent)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Applications')
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Applications')
          AND name = 'CoverLetter'
    )
    BEGIN
        ALTER TABLE dbo.Applications ADD CoverLetter NVARCHAR(MAX) NULL;
        PRINT 'Added Applications.CoverLetter';
    END
END

