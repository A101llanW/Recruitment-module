-- Final Database Health Check
USE HR_Local;
GO

PRINT '╔════════════════════════════════════════════════════════════╗';
PRINT '║         MULTI-TENANT DATABASE HEALTH CHECK                ║';
PRINT '╚════════════════════════════════════════════════════════════╝';
PRINT '';

-- 1. Check all required tables exist
PRINT '1. REQUIRED TABLES:';
DECLARE @RequiredTables TABLE (TableName NVARCHAR(100));
INSERT INTO @RequiredTables VALUES 
    ('Companies'), ('Users'), ('Departments'), ('Positions'), 
    ('Applicants'), ('Applications'), ('Interviews'), ('Questions'),
    ('AuditLogs'), ('LoginAttempts'), ('ImpersonationRequests'),
    ('PasswordResets'), ('Reports'), ('LicenseTransactions');

SELECT 
    t.TableName,
    CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = t.TableName) 
        THEN '✓ EXISTS' 
        ELSE '✗ MISSING' 
    END AS Status
FROM @RequiredTables t;

PRINT '';
PRINT '2. COMPANYID COLUMNS:';
SELECT 
    t.name AS TableName,
    CASE WHEN EXISTS (SELECT * FROM sys.columns WHERE object_id = t.object_id AND name = 'CompanyId')
        THEN '✓ HAS CompanyId'
        ELSE '✗ MISSING CompanyId'
    END AS Status
FROM sys.tables t
WHERE t.name IN ('Users', 'Departments', 'Positions', 'Applicants', 'Applications', 'Interviews', 'Questions', 'AuditLogs', 'LoginAttempts')
ORDER BY t.name;

PRINT '';
PRINT '3. DATA SUMMARY:';
SELECT 
    'Companies' AS Entity,
    COUNT(*) AS Total,
    COUNT(CASE WHEN IsActive = 1 THEN 1 END) AS Active
FROM Companies
UNION ALL
SELECT 'Users', COUNT(*), COUNT(CASE WHEN Role IN ('Admin', 'SuperAdmin') THEN 1 END)
FROM Users
UNION ALL
SELECT 'Departments', COUNT(*), COUNT(CASE WHEN CompanyId IS NOT NULL THEN 1 END)
FROM Departments
UNION ALL
SELECT 'Positions', COUNT(*), COUNT(CASE WHEN IsOpen = 1 THEN 1 END)
FROM Positions
UNION ALL
SELECT 'Applications', COUNT(*), COUNT(CASE WHEN Status = 'Pending' THEN 1 END)
FROM Applications;

PRINT '';
PRINT '4. TENANT ISOLATION CHECK:';
SELECT 
    c.Name AS Company,
    (SELECT COUNT(*) FROM Users WHERE CompanyId = c.Id) AS Users,
    (SELECT COUNT(*) FROM Departments WHERE CompanyId = c.Id) AS Departments,
    (SELECT COUNT(*) FROM Positions WHERE CompanyId = c.Id) AS Positions,
    (SELECT COUNT(*) FROM Applications WHERE CompanyId = c.Id) AS Applications
FROM Companies c
ORDER BY c.Id;

PRINT '';
PRINT '5. SUPERADMIN USERS:';
SELECT UserName, Email, CompanyId, 
    CASE WHEN CompanyId IS NULL THEN '✓ Global Access' ELSE '⚠ Should be NULL' END AS AccessLevel
FROM Users 
WHERE Role = 'SuperAdmin';

PRINT '';
PRINT '6. LICENSE STATUS:';
SELECT 
    Name,
    LicenseExpiryDate,
    DATEDIFF(DAY, GETDATE(), LicenseExpiryDate) AS DaysRemaining,
    CASE 
        WHEN LicenseExpiryDate < GETDATE() THEN '✗ EXPIRED'
        WHEN DATEDIFF(DAY, GETDATE(), LicenseExpiryDate) < 30 THEN '⚠ EXPIRING SOON'
        ELSE '✓ ACTIVE'
    END AS Status
FROM Companies;

PRINT '';
PRINT '╔════════════════════════════════════════════════════════════╗';
PRINT '║                    HEALTH CHECK COMPLETE                   ║';
PRINT '╚════════════════════════════════════════════════════════════╝';
