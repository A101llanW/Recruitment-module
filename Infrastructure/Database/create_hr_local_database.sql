-- Create HR_Local database and basic schema
-- Run this script in SQL Server Management Studio as SA

PRINT '=== Creating HR_Local Database ===';

-- Create the database if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'HR_Local')
BEGIN
    CREATE DATABASE HR_Local;
    PRINT 'HR_Local database created successfully';
END
ELSE
BEGIN
    PRINT 'HR_Local database already exists';
END

-- Switch to the new database
USE HR_Local;
GO

-- Create Companies table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Companies')
BEGIN
    CREATE TABLE Companies (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Slug NVARCHAR(50) NOT NULL UNIQUE,
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME DEFAULT GETDATE()
    );
    PRINT 'Companies table created';
END

-- Create Users table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
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
        FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
    );
    PRINT 'Users table created';
END

-- Insert sample data
IF NOT EXISTS (SELECT * FROM Companies)
BEGIN
    INSERT INTO Companies (Name, Slug, IsActive) VALUES 
    ('Test Company 1', 'U78027609', 1),
    ('Test Company 2', 'Z32815413', 1);
    PRINT 'Sample companies inserted';
END

-- Insert SuperAdmin user
IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'superadmin')
BEGIN
    INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, Role, CompanyId, IsActive) VALUES 
    ('superadmin', 'admin@hrportal.com', 'AQAAAAEAACcQAAAAEKqgkTJF5Y2N5X8F5Y2N5X8F5Y2N5X8F5Y2N5X8F5Y2N5X8', 'Super', 'Admin', 'SuperAdmin', NULL, 1);
    PRINT 'SuperAdmin user created';
END

-- Grant permissions to your Windows account
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'DESKTOP-V47PGAM\allan')
BEGIN
    CREATE USER [DESKTOP-V47PGAM\allan] FOR LOGIN [DESKTOP-V47PGAM\allan];
    ALTER ROLE db_owner ADD MEMBER [DESKTOP-V47PGAM\allan];
    PRINT 'Permissions granted to DESKTOP-V47PGAM\allan';
END

PRINT '=== Database setup complete ===';
PRINT 'HR_Local database is now ready for use';
