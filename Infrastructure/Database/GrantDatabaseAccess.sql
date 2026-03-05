-- Grant SQL Server database access to IIS App Pool and current user
-- Run this script as Administrator or with SQL Server admin rights

USE [master]
GO

-- Create login for current Windows user if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = SUSER_SNAME())
BEGIN
    CREATE LOGIN [$(COMPUTERNAME)\$(USERNAME)] FROM WINDOWS
    PRINT 'Created login for current user'
END
GO

USE [HR_Local]
GO

-- Grant access to current Windows user
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = SUSER_SNAME())
BEGIN
    CREATE USER [$(COMPUTERNAME)\$(USERNAME)] FOR LOGIN [$(COMPUTERNAME)\$(USERNAME)]
    ALTER ROLE db_owner ADD MEMBER [$(COMPUTERNAME)\$(USERNAME)]
    PRINT 'Granted database access to current user'
END
ELSE
BEGIN
    ALTER ROLE db_owner ADD MEMBER [$(COMPUTERNAME)\$(USERNAME)]
    PRINT 'Updated database access for current user'
END
GO

-- Grant access to IIS_IUSRS group (common for IIS/IIS Express)
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'IIS_IUSRS')
BEGIN
    CREATE LOGIN [IIS_IUSRS] FROM WINDOWS
    PRINT 'Created login for IIS_IUSRS'
END
GO

USE [HR_Local]
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'IIS_IUSRS')
BEGIN
    CREATE USER [IIS_IUSRS] FOR LOGIN [IIS_IUSRS]
    ALTER ROLE db_owner ADD MEMBER [IIS_IUSRS]
    PRINT 'Granted database access to IIS_IUSRS'
END
ELSE
BEGIN
    ALTER ROLE db_owner ADD MEMBER [IIS_IUSRS]
    PRINT 'Updated database access for IIS_IUSRS'
END
GO

-- Grant access to App Pool identity (for IIS Express, this might be the current user)
-- Try to add the actual app pool user
DECLARE @appPoolUser NVARCHAR(128) = 'IIS APPPOOL\.NET v4.5'

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = @appPoolUser)
BEGIN
    BEGIN TRY
        EXEC('CREATE LOGIN [' + @appPoolUser + '] FROM WINDOWS')
        PRINT 'Created login for App Pool user'
    END TRY
    BEGIN CATCH
        PRINT 'Could not create login for App Pool user (may not exist): ' + ERROR_MESSAGE()
    END CATCH
END
GO

USE [HR_Local]
GO

DECLARE @appPoolUser NVARCHAR(128) = 'IIS APPPOOL\.NET v4.5'

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = @appPoolUser)
BEGIN
    BEGIN TRY
        EXEC('CREATE USER [' + @appPoolUser + '] FOR LOGIN [' + @appPoolUser + ']')
        EXEC('ALTER ROLE db_owner ADD MEMBER [' + @appPoolUser + ']')
        PRINT 'Granted database access to App Pool user'
    END TRY
    BEGIN CATCH
        PRINT 'Could not grant access to App Pool user: ' + ERROR_MESSAGE()
    END CATCH
END
ELSE
BEGIN
    BEGIN TRY
        EXEC('ALTER ROLE db_owner ADD MEMBER [' + @appPoolUser + ']')
        PRINT 'Updated database access for App Pool user'
    END TRY
    BEGIN CATCH
        PRINT 'Could not update access for App Pool user: ' + ERROR_MESSAGE()
    END CATCH
END
GO

-- Alternative: Grant access to all authenticated users (for development only)
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'BUILTIN\Users')
BEGIN
    CREATE LOGIN [BUILTIN\Users] FROM WINDOWS
    PRINT 'Created login for BUILTIN\Users'
END
GO

USE [HR_Local]
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'BUILTIN\Users')
BEGIN
    CREATE USER [BUILTIN\Users] FOR LOGIN [BUILTIN\Users]
    ALTER ROLE db_owner ADD MEMBER [BUILTIN\Users]
    PRINT 'Granted database access to BUILTIN\Users'
END
ELSE
BEGIN
    ALTER ROLE db_owner ADD MEMBER [BUILTIN\Users]
    PRINT 'Updated database access for BUILTIN\Users'
END
GO

PRINT ''
PRINT '=== Database Access Configuration Complete ==='
PRINT 'If you still have issues, try running IIS Express as your current user account.'
GO
