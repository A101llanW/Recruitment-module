-- Fix Database Schema for Multi-Tenant Support
-- This script adds missing CompanyId columns and creates necessary tables

USE HR_Local;
GO

-- 1. Create Companies table if it doesn't exist
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
        Address NVARCHAR(500) NULL
    );
    PRINT 'Companies table created.';
    
    -- Insert default company
    INSERT INTO Companies (Name, Slug, IsActive, LicenseExpiryDate)
    VALUES ('Default Company', 'default', 1, DATEADD(YEAR, 1, GETDATE()));
    PRINT 'Default company created.';
END
GO

-- 2. Add CompanyId to Users table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Users ADD CompanyId INT NULL;
    PRINT 'CompanyId column added to Users table.';
    
    -- Update existing users to default company
    UPDATE Users SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Slug = 'default');
    PRINT 'Existing users assigned to default company.';
END
GO

-- 3. Add CompanyId to Departments table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Departments') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Departments ADD CompanyId INT NULL;
    PRINT 'CompanyId column added to Departments table.';
    
    -- Update existing departments to default company
    UPDATE Departments SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Slug = 'default');
    PRINT 'Existing departments assigned to default company.';
END
GO

-- 4. Add CompanyId to Positions table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Positions') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Positions ADD CompanyId INT NULL;
    PRINT 'CompanyId column added to Positions table.';
    
    -- Update existing positions to default company
    UPDATE Positions SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Slug = 'default');
    PRINT 'Existing positions assigned to default company.';
END
GO

-- 5. Add CompanyId to Applicants table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Applicants') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Applicants ADD CompanyId INT NULL;
    PRINT 'CompanyId column added to Applicants table.';
    
    -- Update existing applicants to default company
    UPDATE Applicants SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Slug = 'default');
    PRINT 'Existing applicants assigned to default company.';
END
GO

-- 6. Add CompanyId to Applications table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Applications') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Applications ADD CompanyId INT NULL;
    PRINT 'CompanyId column added to Applications table.';
    
    -- Update existing applications to default company
    UPDATE Applications SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Slug = 'default');
    PRINT 'Existing applications assigned to default company.';
END
GO

-- 7. Add CompanyId to Interviews table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Interviews') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Interviews ADD CompanyId INT NULL;
    PRINT 'CompanyId column added to Interviews table.';
    
    -- Update existing interviews to default company
    UPDATE Interviews SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Slug = 'default');
    PRINT 'Existing interviews assigned to default company.';
END
GO

-- 8. Add CompanyId to Questions table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Questions') AND name = 'CompanyId')
BEGIN
    ALTER TABLE Questions ADD CompanyId INT NULL;
    PRINT 'CompanyId column added to Questions table.';
    
    -- Update existing questions to default company
    UPDATE Questions SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Slug = 'default');
    PRINT 'Existing questions assigned to default company.';
END
GO

-- 9. Add CompanyId to AuditLogs table if it doesn't exist
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AuditLogs') AND name = 'CompanyId')
    BEGIN
        ALTER TABLE AuditLogs ADD CompanyId INT NULL;
        PRINT 'CompanyId column added to AuditLogs table.';
        
        -- Update existing audit logs to default company
        UPDATE AuditLogs SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Slug = 'default');
        PRINT 'Existing audit logs assigned to default company.';
    END
END
GO

-- 10. Add CompanyId to LoginAttempts table if it doesn't exist
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'LoginAttempts')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LoginAttempts') AND name = 'CompanyId')
    BEGIN
        ALTER TABLE LoginAttempts ADD CompanyId INT NULL;
        PRINT 'CompanyId column added to LoginAttempts table.';
        
        -- Update existing login attempts to default company
        UPDATE LoginAttempts SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Slug = 'default');
        PRINT 'Existing login attempts assigned to default company.';
    END
END
GO

-- 11. Add Controller column to AuditLogs if missing
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AuditLogs') AND name = 'Controller')
    BEGIN
        ALTER TABLE AuditLogs ADD Controller NVARCHAR(100) NULL;
        PRINT 'Controller column added to AuditLogs table.';
    END
END
GO

PRINT 'Database schema fix completed successfully!';
