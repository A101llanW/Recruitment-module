-- Comprehensive Database Schema Validation
-- This script checks for all columns expected by the application models

USE HR_Local;
GO

PRINT '╔════════════════════════════════════════════════════════════╗';
PRINT '║       COMPREHENSIVE SCHEMA VALIDATION                     ║';
PRINT '╚════════════════════════════════════════════════════════════╝';
PRINT '';

-- Function to check if column exists
DECLARE @TableName NVARCHAR(100);
DECLARE @ColumnName NVARCHAR(100);
DECLARE @Exists BIT;

PRINT '1. USERS TABLE COLUMNS:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT '2. POSITIONS TABLE COLUMNS:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Positions'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT '3. APPLICATIONS TABLE COLUMNS:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Applications'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT '4. INTERVIEWS TABLE COLUMNS:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Interviews'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT '5. CHECKING FOR COMMON MISSING COLUMNS:';

-- Check Users table for all expected columns
DECLARE @MissingColumns TABLE (TableName NVARCHAR(100), ColumnName NVARCHAR(100));

-- Users table expected columns
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'AccessToken')
    INSERT INTO @MissingColumns VALUES ('Users', 'AccessToken');
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RefreshToken')
    INSERT INTO @MissingColumns VALUES ('Users', 'RefreshToken');
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'TokenExpiry')
    INSERT INTO @MissingColumns VALUES ('Users', 'TokenExpiry');
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CompanyId')
    INSERT INTO @MissingColumns VALUES ('Users', 'CompanyId');

-- Positions table expected columns
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Positions') AND name = 'CompanyId')
    INSERT INTO @MissingColumns VALUES ('Positions', 'CompanyId');
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Positions') AND name = 'Location')
    INSERT INTO @MissingColumns VALUES ('Positions', 'Location');

-- Applications table expected columns
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Applications') AND name = 'CompanyId')
    INSERT INTO @MissingColumns VALUES ('Applications', 'CompanyId');

-- Interviews table expected columns
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Interviews') AND name = 'CompanyId')
    INSERT INTO @MissingColumns VALUES ('Interviews', 'CompanyId');

-- Display missing columns
IF EXISTS (SELECT * FROM @MissingColumns)
BEGIN
    PRINT '⚠ MISSING COLUMNS DETECTED:';
    SELECT TableName, ColumnName FROM @MissingColumns;
END
ELSE
BEGIN
    PRINT '✓ All expected columns present!';
END

PRINT '';
PRINT '6. TABLE EXISTENCE CHECK:';
SELECT 
    name AS TableName,
    CASE WHEN EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(name) AND name = 'CompanyId')
        THEN '✓ Has CompanyId'
        ELSE '✗ Missing CompanyId'
    END AS TenantColumn
FROM sys.tables
WHERE name IN ('Users', 'Departments', 'Positions', 'Applicants', 'Applications', 'Interviews', 'Questions', 'AuditLogs', 'LoginAttempts')
ORDER BY name;

PRINT '';
PRINT '╔════════════════════════════════════════════════════════════╗';
PRINT '║              VALIDATION COMPLETE                           ║';
PRINT '╚════════════════════════════════════════════════════════════╝';
