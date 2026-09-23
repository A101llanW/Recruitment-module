-- Incremental schema update since the last live publish package.
-- Baseline: Publish-2026-09-08.zip (2026-09-08).
-- Scope: migrations added by integrated PRs #5-#7, applied in order:
--   1. HR.Web/Migrations/202609210000016_AddCompanySmtpSettings.sql
--   2. HR.Web/Migrations/202609210000017_AddApplicationNotifyRecipients.sql
--   3. HR.Web/Migrations/202609220000018_AddPositionViews.sql
-- Idempotent: safe to re-run. SQL-only; does not update EF migration history.
-- Apply with: tools\dev\Apply-Updates-Since-Publish-2026-09-08.ps1

SET NOCOUNT ON;
PRINT 'Apply-Updates-Since-Publish-2026-09-08: starting.';

-- ---------------------------------------------------------------------------
-- 1. Per-company SMTP settings (202609210000016)
-- ---------------------------------------------------------------------------
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

-- ---------------------------------------------------------------------------
-- 2. New-application notification recipients and access tokens (202609210000017)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.CompanyApplicationNotifyRecipients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyApplicationNotifyRecipients (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_CompanyApplicationNotifyRecipients PRIMARY KEY,
        CompanyId INT NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        Label NVARCHAR(150) NULL,
        AccessMode NVARCHAR(30) NOT NULL CONSTRAINT DF_CompanyApplicationNotifyRecipients_AccessMode DEFAULT (N'LoginRequired'),
        SortOrder INT NOT NULL CONSTRAINT DF_CompanyApplicationNotifyRecipients_SortOrder DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_CompanyApplicationNotifyRecipients_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_CompanyApplicationNotifyRecipients_CreatedDate DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CompanyApplicationNotifyRecipients_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_CompanyApplicationNotifyRecipients_CompanyId ON dbo.CompanyApplicationNotifyRecipients (CompanyId);
    PRINT 'Created dbo.CompanyApplicationNotifyRecipients';
END
ELSE
BEGIN
    PRINT 'dbo.CompanyApplicationNotifyRecipients already exists.';
END

IF OBJECT_ID(N'dbo.ApplicationNotificationAccessTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationNotificationAccessTokens (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_ApplicationNotificationAccessTokens PRIMARY KEY,
        ApplicationId INT NOT NULL,
        RecipientId INT NOT NULL,
        Token NVARCHAR(64) NOT NULL,
        ExpiresAt DATETIME2(0) NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ApplicationNotificationAccessTokens_CreatedDate DEFAULT (SYSUTCDATETIME()),
        RevokedAt DATETIME2(0) NULL,
        CONSTRAINT FK_ApplicationNotificationAccessTokens_Applications FOREIGN KEY (ApplicationId) REFERENCES dbo.Applications (Id) ON DELETE CASCADE,
        CONSTRAINT FK_ApplicationNotificationAccessTokens_Recipients FOREIGN KEY (RecipientId) REFERENCES dbo.CompanyApplicationNotifyRecipients (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_ApplicationNotificationAccessTokens_Token ON dbo.ApplicationNotificationAccessTokens (Token);
    CREATE NONCLUSTERED INDEX IX_ApplicationNotificationAccessTokens_ApplicationId ON dbo.ApplicationNotificationAccessTokens (ApplicationId);
    PRINT 'Created dbo.ApplicationNotificationAccessTokens';
END
ELSE
BEGIN
    PRINT 'dbo.ApplicationNotificationAccessTokens already exists.';
END

-- ---------------------------------------------------------------------------
-- 3. Candidate position view tags and successful login counter (202609220000018)
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Users', N'SuccessfulLoginCount') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD SuccessfulLoginCount INT NOT NULL
        CONSTRAINT DF_Users_SuccessfulLoginCount DEFAULT (0);
    PRINT 'Added dbo.Users.SuccessfulLoginCount';
END
ELSE
BEGIN
    PRINT 'dbo.Users.SuccessfulLoginCount already exists.';
END

IF OBJECT_ID(N'dbo.PositionViews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PositionViews (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_PositionViews PRIMARY KEY,
        UserId INT NOT NULL,
        PositionId INT NOT NULL,
        ViewedAtUtc DATETIME2(0) NOT NULL,
        LoginCountAtView INT NOT NULL,
        IsOpenAtView BIT NOT NULL,
        CONSTRAINT FK_PositionViews_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_PositionViews_Positions FOREIGN KEY (PositionId) REFERENCES dbo.Positions (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_PositionViews_User_Position
        ON dbo.PositionViews (UserId, PositionId);
    PRINT 'Created dbo.PositionViews';
END
ELSE
BEGIN
    PRINT 'dbo.PositionViews already exists.';
END

PRINT 'Apply-Updates-Since-Publish-2026-09-08: finished.';
