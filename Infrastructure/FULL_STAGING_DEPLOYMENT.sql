-- ═══════════════════════════════════════════════════════════════
-- HR SYSTEM: CLEAN PRODUCTION/STAGING DEPLOYMENT SCRIPT
-- ═══════════════════════════════════════════════════════════════
-- This script creates the database and full schema.
-- Includes ONLY the initial SuperAdmin user. No demo data.
-- ═══════════════════════════════════════════════════════════════

USE master;
GO

PRINT '1. Creating/Verifying HR_Local Database...';
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'HR_Local')
BEGIN
    CREATE DATABASE HR_Local;
    PRINT '   + HR_Local database created successfully';
END
GO

USE HR_Local;
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 1: CORE ENGINE TABLES
-- ═══════════════════════════════════════════════════════════════
PRINT '2. Creating Core Engine Tables...';

-- Companies (Tenants)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Companies')
BEGIN
    CREATE TABLE Companies (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Slug NVARCHAR(100) NOT NULL UNIQUE,
        IsActive BIT NOT NULL DEFAULT 1,
        LicenseExpiryDate DATETIME NULL,
        CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
        ContactEmail NVARCHAR(100) NULL,
        ContactPhone NVARCHAR(50) NULL,
        Address NVARCHAR(500) NULL,
        AccessToken NVARCHAR(500) NULL
    );
    PRINT '   + Companies table created';
END

-- Users (Identity)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        Email NVARCHAR(200) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Role NVARCHAR(50) NOT NULL DEFAULT 'User',
        CompanyId INT NULL,
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME DEFAULT GETDATE(),
        PasswordChangeExpiry DATETIME NULL,
        AccessToken NVARCHAR(500) NULL,
        RefreshToken NVARCHAR(500) NULL,
        TokenExpiry DATETIME NULL,
        TwoFactorSecret NVARCHAR(256) NULL,
        IsTwoFactorEnabled BIT NOT NULL DEFAULT 0,
        MfaMethod NVARCHAR(50) NULL,
        TwoFactorCode NVARCHAR(10) NULL,
        TwoFactorExpiry DATETIME NULL,
        CONSTRAINT FK_Users_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
    PRINT '   + Users table created';
END

-- Departments
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Departments')
BEGIN
    CREATE TABLE Departments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        Name NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_Departments_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
END

-- Positions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Positions')
BEGIN
    CREATE TABLE Positions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        DepartmentId INT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        Location NVARCHAR(200) NULL,
        Requirements NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Positions_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id),
        CONSTRAINT FK_Positions_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(Id)
    );
END

-- Applicants
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Applicants')
BEGIN
    CREATE TABLE Applicants (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(200) NOT NULL,
        Phone NVARCHAR(50) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Applicants_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
END

-- Applications
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Applications')
BEGIN
    CREATE TABLE Applications (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        PositionId INT NOT NULL,
        ApplicantId INT NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Applied',
        AppliedDate DATETIME NOT NULL DEFAULT GETDATE(),
        Score DECIMAL(18,2) NULL,
        Notes NVARCHAR(MAX) NULL,
        CONSTRAINT FK_Applications_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id),
        CONSTRAINT FK_Applications_Positions FOREIGN KEY (PositionId) REFERENCES Positions(Id),
        CONSTRAINT FK_Applications_Applicants FOREIGN KEY (ApplicantId) REFERENCES Applicants(Id)
    );
END

-- Interviews
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Interviews')
BEGIN
    CREATE TABLE Interviews (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        ApplicationId INT NOT NULL,
        InterviewerId INT NOT NULL,
        ScheduledAt DATETIME NOT NULL,
        Mode NVARCHAR(50) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Scheduled',
        Notes NVARCHAR(MAX) NULL,
        CONSTRAINT FK_Interviews_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id),
        CONSTRAINT FK_Interviews_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id),
        CONSTRAINT FK_Interviews_Interviewer FOREIGN KEY (InterviewerId) REFERENCES Users(Id)
    );
END

-- Onboardings
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Onboardings')
BEGIN
    CREATE TABLE Onboardings (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ApplicationId INT NOT NULL,
        StartDate DATETIME NOT NULL,
        Tasks NVARCHAR(MAX) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        CONSTRAINT FK_Onboardings_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id)
    );
END

-- ═══════════════════════════════════════════════════════════════
-- STEP 2: QUESTIONNAIRE SYSTEM
-- ═══════════════════════════════════════════════════════════════
PRINT '3. Creating Questionnaire System...';

-- Questions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Questions')
BEGIN
    CREATE TABLE Questions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        Text NVARCHAR(255) NOT NULL,
        Type NVARCHAR(50) NULL,
        Category NVARCHAR(100) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_Questions_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
END

-- QuestionOptions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'QuestionOptions')
BEGIN
    CREATE TABLE QuestionOptions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        QuestionId INT NOT NULL,
        Text NVARCHAR(MAX) NULL,
        Points DECIMAL(18,2) NOT NULL DEFAULT 0,
        CONSTRAINT FK_QuestionOptions_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id)
    );
END

-- PositionQuestions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PositionQuestions')
BEGIN
    CREATE TABLE PositionQuestions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PositionId INT NOT NULL,
        QuestionId INT NOT NULL,
        [Order] INT NOT NULL DEFAULT 0,
        IsRequired BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_PositionQuestions_Positions FOREIGN KEY (PositionId) REFERENCES Positions(Id),
        CONSTRAINT FK_PositionQuestions_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id)
    );
END

-- ApplicationAnswers
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApplicationAnswers')
BEGIN
    CREATE TABLE ApplicationAnswers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ApplicationId INT NOT NULL,
        QuestionId INT NOT NULL,
        AnswerText NVARCHAR(MAX) NULL,
        Score DECIMAL(18,2) NULL,
        CONSTRAINT FK_ApplicationAnswers_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id),
        CONSTRAINT FK_ApplicationAnswers_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id)
    );
END

-- ═══════════════════════════════════════════════════════════════
-- STEP 3: LOGGING & MANAGEMENT
-- ═══════════════════════════════════════════════════════════════
PRINT '4. Creating Logging & Security Tables...';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoginAttempts')
BEGIN
    CREATE TABLE LoginAttempts (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        Username NVARCHAR(100) NOT NULL,
        IPAddress NVARCHAR(45) NOT NULL,
        AttemptTime DATETIME NOT NULL,
        WasSuccessful BIT NOT NULL,
        FailureReason NVARCHAR(MAX) NULL,
        CONSTRAINT FK_LoginAttempts_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE AuditLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        Username NVARCHAR(100) NOT NULL,
        Action NVARCHAR(100) NOT NULL,
        Controller NVARCHAR(100) NOT NULL,
        EntityId NVARCHAR(MAX) NULL,
        OldValues NVARCHAR(MAX) NULL,
        NewValues NVARCHAR(MAX) NULL,
        IPAddress NVARCHAR(45) NOT NULL,
        Timestamp DATETIME NOT NULL,
        UserAgent NVARCHAR(MAX) NULL,
        WasSuccessful BIT NOT NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        CONSTRAINT FK_AuditLogs_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reports')
BEGIN
    CREATE TABLE Reports (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Type NVARCHAR(50) NOT NULL,
        Description NVARCHAR(500) NULL,
        CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
        GeneratedDate DATETIME NULL,
        GeneratedBy NVARCHAR(MAX) NULL,
        FilePath NVARCHAR(500) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        Parameters NVARCHAR(MAX) NULL
    );
END

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

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PasswordResets')
BEGIN
    CREATE TABLE PasswordResets (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        Token NVARCHAR(255) NOT NULL,
        ExpiryDate DATETIME NOT NULL,
        IsUsed BIT NOT NULL DEFAULT 0,
        CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_PasswordResets_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END

GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 4: INITIAL SYSTEM ACCESS (CRITICAL)
-- ═══════════════════════════════════════════════════════════════
PRINT '5. Provisioning Initial SuperAdmin...';

-- admin (SuperAdmin) - Password hash supplied at deploy-time via sqlcmd variable:
--   BOOTSTRAP_ADMIN_PASSWORD_HASH
IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'admin')
BEGIN
    DECLARE @BootstrapPasswordHash NVARCHAR(500) = N'$(BOOTSTRAP_ADMIN_PASSWORD_HASH)';

    IF @BootstrapPasswordHash IS NULL
       OR LTRIM(RTRIM(@BootstrapPasswordHash)) = N''
       OR @BootstrapPasswordHash LIKE N'$(%'
    BEGIN
        PRINT '   ! Skipping admin bootstrap user (missing BOOTSTRAP_ADMIN_PASSWORD_HASH).';
        PRINT '   ! Run the provided Invoke-HostDatabaseScript.ps1 to auto-generate one-time credentials.';
    END
    ELSE
    BEGIN
        INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, Role, CompanyId, IsActive)
        VALUES ('admin', 'admin@nanosoft.com', @BootstrapPasswordHash, 'System', 'Admin', 'SuperAdmin', NULL, 1);
        PRINT '   + Bootstrap SuperAdmin created: admin (password generated by deploy script)';
    END
END
GO

-- Force one-time password change for bootstrap admin when the flag column exists.
IF COL_LENGTH(N'dbo.Users', N'RequirePasswordChange') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'admin')
BEGIN
    UPDATE dbo.Users
    SET RequirePasswordChange = 1
    WHERE Username = N'admin';
    PRINT '   + Enforced one-time password change for bootstrap admin';
END
GO

PRINT '';
PRINT '══════════════════════════════════════════════════════════════';
PRINT '  CLEAN DEPLOYMENT SCRIPT COMPLETED SUCCESSFULLY              ';
PRINT '  No demo data inserted. System is in fresh state.             ';
PRINT '══════════════════════════════════════════════════════════════';
GO
