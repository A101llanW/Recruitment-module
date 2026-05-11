-- Add HasSecondaryStage to Positions (idempotent)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Positions')
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Positions')
          AND name = 'HasSecondaryStage'
    )
    BEGIN
        ALTER TABLE dbo.Positions
            ADD HasSecondaryStage BIT NOT NULL CONSTRAINT DF_Positions_HasSecondaryStage DEFAULT(0);
        PRINT 'Added Positions.HasSecondaryStage';
    END
END
