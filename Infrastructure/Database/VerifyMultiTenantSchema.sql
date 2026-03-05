-- Verify Multi-Tenant Database Schema
USE HR_Local;
GO

PRINT '=== VERIFYING MULTI-TENANT SCHEMA ===';
PRINT '';

-- Check Companies table
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Companies')
    PRINT '✓ Companies table exists';
ELSE
    PRINT '✗ Companies table MISSING';

-- Check CompanyId columns
DECLARE @TableName NVARCHAR(100);
DECLARE @HasCompanyId BIT;

DECLARE table_cursor CURSOR FOR
SELECT name FROM sys.tables 
WHERE name IN ('Users', 'Departments', 'Positions', 'Applicants', 'Applications', 'Interviews', 'Questions', 'AuditLogs', 'LoginAttempts');

OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(@TableName) AND name = 'CompanyId')
        PRINT '✓ ' + @TableName + ' has CompanyId column';
    ELSE
        PRINT '✗ ' + @TableName + ' MISSING CompanyId column';
    
    FETCH NEXT FROM table_cursor INTO @TableName;
END

CLOSE table_cursor;
DEALLOCATE table_cursor;

PRINT '';
PRINT '=== DATA SUMMARY ===';
SELECT 
    (SELECT COUNT(*) FROM Companies) AS TotalCompanies,
    (SELECT COUNT(*) FROM Users) AS TotalUsers,
    (SELECT COUNT(*) FROM Users WHERE CompanyId IS NOT NULL) AS UsersWithCompany,
    (SELECT COUNT(*) FROM Positions) AS TotalPositions,
    (SELECT COUNT(*) FROM Positions WHERE CompanyId IS NOT NULL) AS PositionsWithCompany;

PRINT '';
PRINT '=== COMPANY LIST ===';
SELECT Id, Name, Slug, IsActive, LicenseExpiryDate FROM Companies;
