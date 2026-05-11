IF COL_LENGTH(N'dbo.Users', N'DateOfBirth') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD DateOfBirth DATETIME NULL;
END;
GO

IF COL_LENGTH(N'dbo.Applicants', N'DateOfBirth') IS NULL
BEGIN
    ALTER TABLE dbo.Applicants
        ADD DateOfBirth DATETIME NULL;
END;
GO
