IF COL_LENGTH(N'dbo.Positions', N'PassMarksByStageJson') IS NULL
BEGIN
    ALTER TABLE dbo.Positions ADD PassMarksByStageJson NVARCHAR(4000) NULL;
END
GO
