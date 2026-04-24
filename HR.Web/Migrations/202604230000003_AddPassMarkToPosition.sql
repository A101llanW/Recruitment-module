IF COL_LENGTH(N'dbo.Positions', N'PassMark') IS NULL
BEGIN
    ALTER TABLE dbo.Positions
        ADD PassMark DECIMAL(5, 2) NOT NULL CONSTRAINT DF_Positions_PassMark DEFAULT (50);
END;
GO

IF COL_LENGTH(N'dbo.Positions', N'PassMark') IS NOT NULL
   AND EXISTS (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.Positions')
         AND name = N'PassMark'
         AND is_nullable = 1
   )
BEGIN
    UPDATE dbo.Positions
    SET PassMark = 50
    WHERE PassMark IS NULL;

    ALTER TABLE dbo.Positions
        ALTER COLUMN PassMark DECIMAL(5, 2) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Positions')
      AND c.name = N'PassMark'
)
BEGIN
    ALTER TABLE dbo.Positions
        ADD CONSTRAINT DF_Positions_PassMark DEFAULT (50) FOR PassMark;
END;
GO
