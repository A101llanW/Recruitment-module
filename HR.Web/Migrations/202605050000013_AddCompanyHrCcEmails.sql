-- Company-scoped HR / internal CC addresses for candidate emails (idempotent).

IF OBJECT_ID(N'dbo.CompanyHrCcEmails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyHrCcEmails (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_CompanyHrCcEmails PRIMARY KEY,
        CompanyId INT NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        Label NVARCHAR(150) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_CompanyHrCcEmails_SortOrder DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_CompanyHrCcEmails_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_CompanyHrCcEmails_CreatedDate DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CompanyHrCcEmails_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_CompanyHrCcEmails_CompanyId ON dbo.CompanyHrCcEmails (CompanyId);
    PRINT 'Created dbo.CompanyHrCcEmails';
END
ELSE
BEGIN
    PRINT 'dbo.CompanyHrCcEmails already exists.';
END
