-- Per-company SMTP settings for tenant-scoped outbound email (idempotent).

IF OBJECT_ID(N'dbo.CompanySmtpSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanySmtpSettings (
        CompanyId INT NOT NULL CONSTRAINT PK_CompanySmtpSettings PRIMARY KEY,
        IsEnabled BIT NOT NULL CONSTRAINT DF_CompanySmtpSettings_IsEnabled DEFAULT (0),
        SmtpHost NVARCHAR(255) NULL,
        SmtpPort INT NOT NULL CONSTRAINT DF_CompanySmtpSettings_SmtpPort DEFAULT (587),
        SmtpUser NVARCHAR(255) NULL,
        SmtpPasswordEncrypted NVARCHAR(1024) NULL,
        SmtpEnableSsl BIT NOT NULL CONSTRAINT DF_CompanySmtpSettings_SmtpEnableSsl DEFAULT (1),
        FromEmail NVARCHAR(255) NULL,
        FromName NVARCHAR(150) NULL,
        UpdatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_CompanySmtpSettings_UpdatedDate DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CompanySmtpSettings_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id) ON DELETE CASCADE
    );

    PRINT 'Created dbo.CompanySmtpSettings';
END
ELSE
BEGIN
    PRINT 'dbo.CompanySmtpSettings already exists.';
END
