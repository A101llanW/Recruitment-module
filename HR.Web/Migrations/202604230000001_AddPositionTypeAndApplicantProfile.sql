IF COL_LENGTH(N'dbo.Positions', N'IsTechnical') IS NULL
BEGIN
    ALTER TABLE dbo.Positions
        ADD IsTechnical BIT NULL;
END;
GO

IF OBJECT_ID(N'dbo.ApplicantProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicantProfiles
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_ApplicantProfiles PRIMARY KEY,
        ApplicantId INT NOT NULL,
        Location NVARCHAR(200) NULL,
        TotalYearsExperience DECIMAL(5, 2) NULL,
        RelevantYearsExperience DECIMAL(5, 2) NULL,
        MostRecentCompany NVARCHAR(200) NULL,
        MostRecentTitle NVARCHAR(150) NULL,
        MostRecentStartDate DATETIME NULL,
        MostRecentEndDate DATETIME NULL,
        SecondMostRecentCompany NVARCHAR(200) NULL,
        SecondMostRecentTitle NVARCHAR(150) NULL,
        SecondMostRecentStartDate DATETIME NULL,
        SecondMostRecentEndDate DATETIME NULL,
        EmploymentType NVARCHAR(50) NULL,
        Skills NVARCHAR(1000) NULL,
        Competencies NVARCHAR(1000) NULL,
        EducationDegree NVARCHAR(200) NULL,
        EducationInstitution NVARCHAR(200) NULL,
        KeyAchievement NVARCHAR(500) NULL,
        Certifications NVARCHAR(500) NULL,
        PortfolioUrl NVARCHAR(300) NULL,
        WorkAuthorization BIT NOT NULL DEFAULT 0,
        NoticePeriod NVARCHAR(100) NULL,
        CreatedOn DATETIME NOT NULL,
        UpdatedOn DATETIME NOT NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_ApplicantProfiles_Applicants_ApplicantId'
)
BEGIN
    ALTER TABLE dbo.ApplicantProfiles
        ADD CONSTRAINT FK_ApplicantProfiles_Applicants_ApplicantId
        FOREIGN KEY (ApplicantId) REFERENCES dbo.Applicants (Id)
        ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ApplicantProfile_Applicant'
      AND object_id = OBJECT_ID(N'dbo.ApplicantProfiles')
)
BEGIN
    CREATE UNIQUE INDEX IX_ApplicantProfile_Applicant
        ON dbo.ApplicantProfiles (ApplicantId);
END;
GO
