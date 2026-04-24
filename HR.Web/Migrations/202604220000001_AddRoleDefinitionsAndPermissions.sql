IF OBJECT_ID(N'dbo.RoleDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoleDefinitions
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_RoleDefinitions PRIMARY KEY,
        CompanyId INT NULL,
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        CreatedByUserName NVARCHAR(100) NOT NULL,
        CreatedDate DATETIME NOT NULL,
        IsActive BIT NOT NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_RoleDefinitions_Companies_CompanyId'
)
BEGIN
    ALTER TABLE dbo.RoleDefinitions
        ADD CONSTRAINT FK_RoleDefinitions_Companies_CompanyId
        FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_RoleDefinitions_CompanyId'
      AND object_id = OBJECT_ID(N'dbo.RoleDefinitions')
)
BEGIN
    CREATE INDEX IX_RoleDefinitions_CompanyId
        ON dbo.RoleDefinitions (CompanyId);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_RoleDefinitions_Name'
      AND object_id = OBJECT_ID(N'dbo.RoleDefinitions')
)
BEGIN
    CREATE INDEX IX_RoleDefinitions_Name
        ON dbo.RoleDefinitions (Name);
END;
GO

IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissions
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_RolePermissions PRIMARY KEY,
        RoleDefinitionId INT NOT NULL,
        ModuleKey NVARCHAR(50) NOT NULL,
        AccessLevel NVARCHAR(20) NOT NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_RolePermissions_RoleDefinitions_RoleDefinitionId'
)
BEGIN
    ALTER TABLE dbo.RolePermissions
        ADD CONSTRAINT FK_RolePermissions_RoleDefinitions_RoleDefinitionId
        FOREIGN KEY (RoleDefinitionId) REFERENCES dbo.RoleDefinitions (Id);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_RolePermission_Module'
      AND object_id = OBJECT_ID(N'dbo.RolePermissions')
)
BEGIN
    CREATE UNIQUE INDEX IX_RolePermission_Module
        ON dbo.RolePermissions (RoleDefinitionId, ModuleKey);
END;
GO

IF COL_LENGTH(N'dbo.Users', N'RoleDefinitionId') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD RoleDefinitionId INT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Users_RoleDefinitionId'
      AND object_id = OBJECT_ID(N'dbo.Users')
)
BEGIN
    CREATE INDEX IX_Users_RoleDefinitionId
        ON dbo.Users (RoleDefinitionId);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Users_RoleDefinitions_RoleDefinitionId'
)
BEGIN
    ALTER TABLE dbo.Users
        ADD CONSTRAINT FK_Users_RoleDefinitions_RoleDefinitionId
        FOREIGN KEY (RoleDefinitionId) REFERENCES dbo.RoleDefinitions (Id);
END;
GO
