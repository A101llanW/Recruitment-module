IF COL_LENGTH(N'dbo.Users', N'PrivacyAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD PrivacyAcceptedAt DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'TermsAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TermsAcceptedAt DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'PrivacyVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD PrivacyVersion NVARCHAR(20) NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'TermsVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TermsVersion NVARCHAR(20) NULL;
END
GO

IF COL_LENGTH(N'dbo.Applicants', N'PrivacyAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Applicants ADD PrivacyAcceptedAt DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Applicants', N'TermsAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Applicants ADD TermsAcceptedAt DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Applicants', N'PrivacyVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Applicants ADD PrivacyVersion NVARCHAR(20) NULL;
END
GO

IF COL_LENGTH(N'dbo.Applicants', N'TermsVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Applicants ADD TermsVersion NVARCHAR(20) NULL;
END
GO
