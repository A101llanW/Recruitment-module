-- Adds tracking for failed-candidate outreach email (Applications admin module).
IF COL_LENGTH(N'dbo.Applications', N'FailedCandidateEmailSentAt') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD FailedCandidateEmailSentAt DATETIME NULL;
END
GO
