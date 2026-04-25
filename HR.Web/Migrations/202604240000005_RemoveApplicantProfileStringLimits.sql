IF OBJECT_ID(N'dbo.ApplicantProfiles', N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN Location NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN MostRecentCompany NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN MostRecentTitle NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN SecondMostRecentCompany NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN SecondMostRecentTitle NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN EmploymentType NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN Skills NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN Competencies NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN EducationDegree NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN EducationInstitution NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN KeyAchievement NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN Certifications NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN PortfolioUrl NVARCHAR(MAX) NULL;
    ALTER TABLE dbo.ApplicantProfiles ALTER COLUMN NoticePeriod NVARCHAR(MAX) NULL;
END;
GO
