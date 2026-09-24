-- Idempotent custom report definitions (company-scoped).
IF OBJECT_ID(N'dbo.CustomReportDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomReportDefinitions (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_CustomReportDefinitions PRIMARY KEY,
        CompanyId INT NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        DatasetKey NVARCHAR(50) NOT NULL,
        ConfigJson NVARCHAR(MAX) NOT NULL,
        CreatedBy NVARCHAR(100) NOT NULL,
        CreatedOn DATETIME NOT NULL CONSTRAINT DF_CustomReportDefinitions_CreatedOn DEFAULT (GETUTCDATE()),
        UpdatedBy NVARCHAR(100) NULL,
        UpdatedOn DATETIME NULL,
        CONSTRAINT FK_CustomReportDefinitions_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id)
    );
    CREATE NONCLUSTERED INDEX IX_CustomReportDefinitions_CompanyId_CreatedOn
        ON dbo.CustomReportDefinitions (CompanyId, CreatedOn DESC);
END
GO
