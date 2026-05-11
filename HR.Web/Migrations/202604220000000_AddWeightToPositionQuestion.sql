-- Add Weight to PositionQuestions (idempotent)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PositionQuestions')
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.PositionQuestions')
          AND name = 'Weight'
    )
    BEGIN
        ALTER TABLE dbo.PositionQuestions
            ADD Weight DECIMAL(18, 2) NULL;
        PRINT 'Added PositionQuestions.Weight';
    END
END
GO
