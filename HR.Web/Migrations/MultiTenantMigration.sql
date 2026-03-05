-- Multi-Tenant Migration Script
-- This script adds the Companies table and associates existing data with a default company.

-- 1. Create Companies table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Companies')
BEGIN
    CREATE TABLE Companies (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Slug NVARCHAR(50) NOT NULL UNIQUE,
        IsActive BIT NOT NULL DEFAULT 1,
        LicenseExpiryDate DATETIME NULL,
        CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Companies table created.';
END
GO

-- 2. Seed default company
IF NOT EXISTS (SELECT * FROM Companies WHERE Slug = 'default')
BEGIN
    INSERT INTO Companies (Name, Slug, IsActive, LicenseExpiryDate)
    VALUES ('Nanosoft Corporation', 'default', 1, DATEADD(year, 1, GETDATE()));
    PRINT 'Default company seeded.';
END
GO

-- 2.5 Ensure Users table has required columns (PasswordHash, etc.)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PasswordHash')
    BEGIN
        ALTER TABLE Users ADD PasswordHash NVARCHAR(256) NULL;
        PRINT 'Added PasswordHash to Users.';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RequirePasswordChange')
    BEGIN
        ALTER TABLE Users ADD RequirePasswordChange BIT NOT NULL DEFAULT 0;
        PRINT 'Added RequirePasswordChange to Users.';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LastPasswordChange')
    BEGIN
        ALTER TABLE Users ADD LastPasswordChange DATETIME NULL;
        PRINT 'Added LastPasswordChange to Users.';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PasswordChangeExpiry')
    BEGIN
        ALTER TABLE Users ADD PasswordChangeExpiry DATETIME NULL;
        PRINT 'Added PasswordChangeExpiry to Users.';
    END
END
GO

-- 3. Add CompanyId to related tables
DECLARE @DefaultCompanyId INT = (SELECT Id FROM Companies WHERE Slug = 'default');
DECLARE @TargetTables TABLE (TableName NVARCHAR(256));
INSERT INTO @TargetTables (TableName)
VALUES ('Users'), ('Departments'), ('Positions'), ('Applicants'), ('Applications'), 
       ('Interviews'), ('Onboardings'), ('Questions'), ('AuditLogs'), ('LoginAttempts'), ('Reports'), ('PasswordResets'), ('LicenseTransactions');

DECLARE @TableName NVARCHAR(256);
DECLARE CUR CURSOR FOR SELECT TableName FROM @TargetTables;

OPEN CUR;
FETCH NEXT FROM CUR INTO @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT * FROM sys.tables WHERE name = @TableName)
    BEGIN
        DECLARE @Sql NVARCHAR(MAX);
        
        -- Add Column if not exists
        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(@TableName) AND name = 'CompanyId')
        BEGIN
            SET @Sql = 'ALTER TABLE ' + @TableName + ' ADD CompanyId INT NULL;';
            EXEC sp_executesql @Sql;
            
            -- Update existing records to default company
            SET @Sql = 'UPDATE ' + @TableName + ' SET CompanyId = ' + CAST(@DefaultCompanyId AS NVARCHAR(10)) + ' WHERE CompanyId IS NULL;';
            EXEC sp_executesql @Sql;
            
            -- Add Foreign Key
            -- Check if constraint exists first
            DECLARE @ConstraintName NVARCHAR(256) = 'FK_' + @TableName + '_Companies';
            IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = @ConstraintName)
            BEGIN
                SET @Sql = 'ALTER TABLE ' + @TableName + ' ADD CONSTRAINT ' + @ConstraintName + ' FOREIGN KEY (CompanyId) REFERENCES Companies(Id);';
                EXEC sp_executesql @Sql;
            END
            
            PRINT 'Updated table ' + @TableName + ' with CompanyId.';
        END
    END
    FETCH NEXT FROM CUR INTO @TableName;
END

CLOSE CUR;
DEALLOCATE CUR;
GO

-- 4. Seed SuperAdmin user
IF NOT EXISTS (SELECT * FROM Users WHERE Role = 'SuperAdmin')
BEGIN
    -- Using a placeholder hash for 'SuperAdmin123!' 
    INSERT INTO Users (UserName, Email, Role, PasswordHash, RequirePasswordChange, CompanyId)
    VALUES ('superadmin', 'superadmin@system.com', 'SuperAdmin', '100000.vIK3+WvQ1B9L8g9f7u2rUA==.z0R1wJ6X8k3mN5pQ9vL2A==', 0, NULL);
    PRINT 'SuperAdmin user seeded.';
END
GO
