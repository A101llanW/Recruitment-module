-- New-application notification recipients and public read-only access tokens (idempotent).

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
