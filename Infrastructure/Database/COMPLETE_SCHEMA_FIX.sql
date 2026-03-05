-- ═══════════════════════════════════════════════════════════════
-- COMPLETE DATABASE SCHEMA FIX FOR HR MULTI-TENANT SYSTEM
-- Run this script to fix all schema issues in one go
-- ═══════════════════════════════════════════════════════════════

USE HR_Local;
GO

PRINT '╔════════════════════════════════════════════════════════════╗';
PRINT '║   COMPLETE MULTI-TENANT SCHEMA MIGRATION                  ║';
PRINT '║   This script will fix all schema issues                  ║';
PRINT '╚════════════════════════════════════════════════════════════╝';
PRINT '';

-- ═══════════════════════════════════════════════════════════════
-- STEP 1: CREATE CORE & SECURITY TABLES
-- ═══════════════════════════════════════════════════════════════
PRINT '1. Creating missing tables...';

-- Companies (Core)
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
        AccessToken NVARCHAR(500) NULL -- Used for specific integrations
    );
    PRINT '   + Companies table created';
    
    -- Insert default company
    INSERT INTO Companies (Name, Slug, IsActive, LicenseExpiryDate)
    VALUES ('Default Company', 'default', 1, DATEADD(YEAR, 1, GETDATE()));
    PRINT '   + Default company created';
END
ELSE
BEGIN
    PRINT '   + Companies table exists';
    -- Ensure AccessToken exists on Companies
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'AccessToken')
    BEGIN
        ALTER TABLE Companies ADD AccessToken NVARCHAR(500) NULL;
        PRINT '   + Added AccessToken to Companies';
    END
END

-- LoginAttempts (Security)
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
    PRINT '   + LoginAttempts table created';
END

-- AuditLogs (Auditing)
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
    PRINT '   + AuditLogs table created';
END

-- PasswordResets (Security)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PasswordResets')
BEGIN
    -- Only create if Users likely exists or check first. Assuming Core Tables exist.
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
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
        PRINT '   + PasswordResets table created';
    END
    ELSE
    BEGIN
        PRINT '   ! Skipping PasswordResets (Users table missing)';
    END
END

-- Reports (Functional)
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
    PRINT '   + Reports table created';
END

-- LicenseTransactions (Billing/Access)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LicenseTransactions')
BEGIN
    CREATE TABLE LicenseTransactions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        ExecutedBy NVARCHAR(100) NOT NULL,
        TransactionDate DATETIME NOT NULL DEFAULT GETDATE(),
        PreviousExpiry DATETIME NULL,
        NewExpiry DATETIME NOT NULL,
        ExtendedByUnit NVARCHAR(20) NULL,
        ExtendedByValue INT NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        CONSTRAINT FK_LicenseTransactions_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
    PRINT '   + LicenseTransactions table created';
END

-- ImpersonationRequests (Admin)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImpersonationRequests')
BEGIN
    CREATE TABLE ImpersonationRequests (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        RequestedBy NVARCHAR(100) NOT NULL,
        RequestedFrom NVARCHAR(100) NOT NULL,
        RequestDate DATETIME NOT NULL DEFAULT GETDATE(),
        Reason NVARCHAR(MAX) NULL,
        Status INT NOT NULL DEFAULT 0,
        DecisionDate DATETIME NULL,
        AdminNotes NVARCHAR(MAX) NULL,
        ExpiryDate DATETIME NULL,
        CONSTRAINT FK_ImpersonationRequests_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
    PRINT '   + ImpersonationRequests table created';
END

GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 2: ADD COMPANYID TO TENANT TABLES (IF MISSING)
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '2. Verifying CompanyId columns...';

DECLARE @DefaultCompanyId INT;
SELECT TOP 1 @DefaultCompanyId = Id FROM Companies WHERE Slug = 'default';

-- Helper logic for Users, Departments, Positions, etc.
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Users') AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Users ADD CompanyId INT NULL;
    UPDATE Users SET CompanyId = @DefaultCompanyId;
    PRINT '   + CompanyId added to Users';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Departments') AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Departments') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Departments ADD CompanyId INT NULL;
    UPDATE Departments SET CompanyId = @DefaultCompanyId;
    PRINT '   + CompanyId added to Departments';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Positions') AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Positions') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Positions ADD CompanyId INT NULL;
    UPDATE Positions SET CompanyId = @DefaultCompanyId;
    PRINT '   + CompanyId added to Positions';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Applicants') AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Applicants') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Applicants ADD CompanyId INT NULL;
    UPDATE Applicants SET CompanyId = @DefaultCompanyId;
    PRINT '   + CompanyId added to Applicants';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Applications') AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Applications') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Applications ADD CompanyId INT NULL;
    UPDATE Applications SET CompanyId = @DefaultCompanyId;
    PRINT '   + CompanyId added to Applications';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Interviews') AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Interviews') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Interviews ADD CompanyId INT NULL;
    UPDATE Interviews SET CompanyId = @DefaultCompanyId;
    PRINT '   + CompanyId added to Interviews';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Questions') AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Questions') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Questions ADD CompanyId INT NULL;
    UPDATE Questions SET CompanyId = @DefaultCompanyId;
    PRINT '   + CompanyId added to Questions';
END

GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 3: ADD MISSING COLUMNS TO USERS
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '3. Adding missing columns to Users...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'AccessToken')
BEGIN
    ALTER TABLE Users ADD AccessToken NVARCHAR(500) NULL;
    PRINT '   + AccessToken added';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RefreshToken')
BEGIN
    ALTER TABLE Users ADD RefreshToken NVARCHAR(500) NULL;
    PRINT '   + RefreshToken added';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'TokenExpiry')
BEGIN
    ALTER TABLE Users ADD TokenExpiry DATETIME NULL;
    PRINT '   + TokenExpiry added';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PasswordChangeExpiry')
BEGIN
    ALTER TABLE Users ADD PasswordChangeExpiry DATETIME NULL;
    PRINT '   + PasswordChangeExpiry added';
END
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 4: ADD MISSING COLUMNS TO POSITIONS
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '4. Adding missing columns to Positions...';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Positions') 
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Positions') AND name = 'Location')
BEGIN
    ALTER TABLE Positions ADD Location NVARCHAR(200) NULL;
    PRINT '   + Location added to Positions';
END
ELSE IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Positions')
BEGIN
    PRINT '   + Location column already exists in Positions';
END
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 5: CREATE/VERIFY QUESTIONS TABLE
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '5. Creating/verifying Questions table...';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Questions')
BEGIN
    CREATE TABLE Questions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        Text NVARCHAR(255) NOT NULL,
        Type NVARCHAR(50) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_Questions_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
    PRINT '   + Questions table created';
END
ELSE
BEGIN
    PRINT '   + Questions table exists';
    
    -- Add missing columns
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Questions') AND name = 'CompanyId')
    BEGIN
        ALTER TABLE Questions ADD CompanyId INT NULL;
        DECLARE @DefaultCompanyId2 INT;
        SELECT TOP 1 @DefaultCompanyId2 = Id FROM Companies WHERE Slug = 'default';
        UPDATE Questions SET CompanyId = @DefaultCompanyId2;
        PRINT '   + CompanyId added to Questions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Questions') AND name = 'Text')
    BEGIN
        ALTER TABLE Questions ADD Text NVARCHAR(255) NOT NULL;
        PRINT '   + Text added to Questions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Questions') AND name = 'Type')
    BEGIN
        ALTER TABLE Questions ADD Type NVARCHAR(50) NULL;
        PRINT '   + Type added to Questions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Questions') AND name = 'IsActive')
    BEGIN
        ALTER TABLE Questions ADD IsActive BIT NOT NULL DEFAULT 1;
        PRINT '   + IsActive added to Questions';
    END
END
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 6: CREATE/VERIFY QUESTIONOPTIONS TABLE
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '6. Creating/verifying QuestionOptions table...';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'QuestionOptions')
BEGIN
    CREATE TABLE QuestionOptions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        QuestionId INT NOT NULL,
        Text NVARCHAR(MAX) NULL,
        Points DECIMAL(18,2) NOT NULL DEFAULT 0,
        CONSTRAINT FK_QuestionOptions_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id)
    );
    PRINT '   + QuestionOptions table created';
END
ELSE
BEGIN
    PRINT '   + QuestionOptions table exists';
    
    -- Add missing columns
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('QuestionOptions') AND name = 'QuestionId')
    BEGIN
        ALTER TABLE QuestionOptions ADD QuestionId INT NOT NULL;
        PRINT '   + QuestionId added to QuestionOptions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('QuestionOptions') AND name = 'Text')
    BEGIN
        ALTER TABLE QuestionOptions ADD Text NVARCHAR(MAX) NULL;
        PRINT '   + Text added to QuestionOptions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('QuestionOptions') AND name = 'Points')
    BEGIN
        ALTER TABLE QuestionOptions ADD Points DECIMAL(18,2) NOT NULL DEFAULT 0;
        PRINT '   + Points added to QuestionOptions';
    END
END
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 7: CREATE/VERIFY POSITIONQUESTIONS TABLE
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '7. Creating/verifying PositionQuestions table...';

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
    PRINT '   + PositionQuestions table created';
END
ELSE
BEGIN
    PRINT '   + PositionQuestions table exists';
    
    -- Add missing columns
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PositionQuestions') AND name = 'PositionId')
    BEGIN
        ALTER TABLE PositionQuestions ADD PositionId INT NOT NULL;
        PRINT '   + PositionId added to PositionQuestions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PositionQuestions') AND name = 'QuestionId')
    BEGIN
        ALTER TABLE PositionQuestions ADD QuestionId INT NOT NULL;
        PRINT '   + QuestionId added to PositionQuestions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PositionQuestions') AND name = 'Order')
    BEGIN
        ALTER TABLE PositionQuestions ADD [Order] INT NOT NULL DEFAULT 0;
        PRINT '   + Order added to PositionQuestions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PositionQuestions') AND name = 'IsRequired')
    BEGIN
        ALTER TABLE PositionQuestions ADD IsRequired BIT NOT NULL DEFAULT 1;
        PRINT '   + IsRequired added to PositionQuestions';
    END
END
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 8: CREATE/VERIFY APPLICATIONANSWERS TABLE
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '8. Creating/verifying ApplicationAnswers table...';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApplicationAnswers')
BEGIN
    CREATE TABLE ApplicationAnswers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ApplicationId INT NOT NULL,
        QuestionId INT NOT NULL,
        AnswerText NVARCHAR(MAX) NULL,
        CONSTRAINT FK_ApplicationAnswers_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id),
        CONSTRAINT FK_ApplicationAnswers_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id)
    );
    PRINT '   + ApplicationAnswers table created';
END
ELSE
BEGIN
    PRINT '   + ApplicationAnswers table exists';
    
    -- Add missing columns
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ApplicationAnswers') AND name = 'ApplicationId')
    BEGIN
        ALTER TABLE ApplicationAnswers ADD ApplicationId INT NOT NULL;
        PRINT '   + ApplicationId added to ApplicationAnswers';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ApplicationAnswers') AND name = 'QuestionId')
    BEGIN
        ALTER TABLE ApplicationAnswers ADD QuestionId INT NOT NULL;
        PRINT '   + QuestionId added to ApplicationAnswers';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ApplicationAnswers') AND name = 'AnswerText')
    BEGIN
        ALTER TABLE ApplicationAnswers ADD AnswerText NVARCHAR(MAX) NULL;
        PRINT '   + AnswerText added to ApplicationAnswers';
    END
END
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 9: CREATE/VERIFY POSITIONQUESTIONOPTIONS TABLE
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '9. Creating/verifying PositionQuestionOptions table...';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PositionQuestionOptions')
BEGIN
    CREATE TABLE PositionQuestionOptions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PositionQuestionId INT NOT NULL,
        QuestionOptionId INT NOT NULL,
        Points DECIMAL(18,2) NULL,
        CONSTRAINT FK_PositionQuestionOptions_PositionQuestions FOREIGN KEY (PositionQuestionId) REFERENCES PositionQuestions(Id),
        CONSTRAINT FK_PositionQuestionOptions_QuestionOptions FOREIGN KEY (QuestionOptionId) REFERENCES QuestionOptions(Id)
    );
    PRINT '   + PositionQuestionOptions table created';
END
ELSE
BEGIN
    PRINT '   + PositionQuestionOptions table exists';
    
    -- Add missing columns
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PositionQuestionOptions') AND name = 'PositionQuestionId')
    BEGIN
        ALTER TABLE PositionQuestionOptions ADD PositionQuestionId INT NOT NULL;
        PRINT '   + PositionQuestionId added to PositionQuestionOptions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PositionQuestionOptions') AND name = 'QuestionOptionId')
    BEGIN
        ALTER TABLE PositionQuestionOptions ADD QuestionOptionId INT NOT NULL;
        PRINT '   + QuestionOptionId added to PositionQuestionOptions';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PositionQuestionOptions') AND name = 'Points')
    BEGIN
        ALTER TABLE PositionQuestionOptions ADD Points DECIMAL(18,2) NULL;
        PRINT '   + Points added to PositionQuestionOptions';
    END
END
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 10: VERIFY INTERVIEWS TABLE COLUMNS
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '10. Verifying Interviews table columns...';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Interviews')
BEGIN
    -- Verify InterviewerId exists
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Interviews') AND name = 'InterviewerId')
    BEGIN
        ALTER TABLE Interviews ADD InterviewerId INT NOT NULL;
        PRINT '   + InterviewerId added to Interviews';
    END
    ELSE
    BEGIN
        PRINT '   + InterviewerId column exists in Interviews';
    END
    
    -- Verify ScheduledAt exists
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Interviews') AND name = 'ScheduledAt')
    BEGIN
        ALTER TABLE Interviews ADD ScheduledAt DATETIME NOT NULL;
        PRINT '   + ScheduledAt added to Interviews';
    END
    ELSE
    BEGIN
        PRINT '   + ScheduledAt column exists in Interviews';
    END
    
    -- Verify Mode exists
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Interviews') AND name = 'Mode')
    BEGIN
        ALTER TABLE Interviews ADD Mode NVARCHAR(50) NULL;
        PRINT '   + Mode added to Interviews';
    END
    
    -- Verify Notes exists
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Interviews') AND name = 'Notes')
    BEGIN
        ALTER TABLE Interviews ADD Notes NVARCHAR(500) NULL;
        PRINT '   + Notes added to Interviews';
    END
END
ELSE
BEGIN
    PRINT '   ! Interviews table does not exist';
END
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 11: FIX SUPERADMIN USERS
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '11. Configuring SuperAdmin users...';

UPDATE Users 
SET CompanyId = NULL 
WHERE Role = 'SuperAdmin';
PRINT '   + SuperAdmin users verified (CompanyId = NULL)';
GO

-- ═══════════════════════════════════════════════════════════════
-- STEP 12: VERIFICATION
-- ═══════════════════════════════════════════════════════════════
PRINT '';
PRINT '╔════════════════════════════════════════════════════════════╗';
PRINT '║                    VERIFICATION                            ║';
PRINT '╚════════════════════════════════════════════════════════════╝';
PRINT '';

PRINT 'Companies:';
SELECT Id, Name, Slug, IsActive, LicenseExpiryDate FROM Companies;

PRINT '';
PRINT 'Table Check:';
SELECT 
    t.name AS TableName,
    CASE WHEN t.object_id IS NOT NULL THEN '✓ OK' ELSE 'MISSING' END AS Status,
    (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = t.object_id) AS ColumnCount
FROM (VALUES 
    ('Users'), ('Companies'), ('LoginAttempts'), ('AuditLogs'), 
    ('PasswordResets'), ('Reports'), ('LicenseTransactions'), 
    ('ImpersonationRequests'), ('Positions'), ('Applications'),
    ('Interviews'), ('Questions'), ('QuestionOptions'), 
    ('PositionQuestions'), ('ApplicationAnswers'), ('PositionQuestionOptions')
) AS names(name)
LEFT JOIN sys.tables t ON t.name = names.name;

PRINT '';
PRINT '╔════════════════════════════════════════════════════════════╗';
PRINT '║              MIGRATION COMPLETED SUCCESSFULLY              ║';
PRINT '╚════════════════════════════════════════════════════════════╝';
