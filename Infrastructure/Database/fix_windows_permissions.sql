-- Grant permissions to Windows user for HR_Local database
-- Run this script in SQL Server Management Studio as SA

PRINT '=== Granting permissions to DESKTOP-V47PGAM\allan ===';

-- Create login for your Windows account if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'DESKTOP-V47PGAM\allan')
BEGIN
    CREATE LOGIN [DESKTOP-V47PGAM\allan] FROM WINDOWS;
    PRINT 'Login created for DESKTOP-V47PGAM\allan';
END
ELSE
BEGIN
    PRINT 'Login already exists for DESKTOP-V47PGAM\allan';
END

-- Switch to HR_Local database
USE HR_Local;
GO

-- Create user in the database if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'DESKTOP-V47PGAM\allan')
BEGIN
    CREATE USER [DESKTOP-V47PGAM\allan] FOR LOGIN [DESKTOP-V47PGAM\allan];
    PRINT 'User created in HR_Local database';
END
ELSE
BEGIN
    PRINT 'User already exists in HR_Local database';
END

-- Grant db_owner role (full permissions)
ALTER ROLE db_owner ADD MEMBER [DESKTOP-V47PGAM\allan];
PRINT 'Granted db_owner role to DESKTOP-V47PGAM\allan';

-- Also grant to IIS Application Pool (for web access)
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'IIS APPPOOL\DefaultAppPool')
BEGIN
    CREATE LOGIN [IIS APPPOOL\DefaultAppPool] FROM WINDOWS;
    PRINT 'Login created for IIS APPPOOL\DefaultAppPool';
END

USE HR_Local;
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'IIS APPPOOL\DefaultAppPool')
BEGIN
    CREATE USER [IIS APPPOOL\DefaultAppPool] FOR LOGIN [IIS APPPOOL\DefaultAppPool];
    PRINT 'User created in HR_Local database for IIS';
END

ALTER ROLE db_owner ADD MEMBER [IIS APPPOOL\DefaultAppPool];
PRINT 'Granted db_owner role to IIS APPPOOL\DefaultAppPool';

PRINT '=== Permissions setup complete ===';
PRINT 'You can now connect to HR_Local database using Windows Authentication';
