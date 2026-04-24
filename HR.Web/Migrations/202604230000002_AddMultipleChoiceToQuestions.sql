IF COL_LENGTH(N'dbo.Questions', N'AllowMultipleChoices') IS NULL
BEGIN
    ALTER TABLE dbo.Questions
        ADD AllowMultipleChoices BIT NOT NULL CONSTRAINT DF_Questions_AllowMultipleChoices DEFAULT 0;
END;
GO
