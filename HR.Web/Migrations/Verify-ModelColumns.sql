/*
  Compares dbo tables to HR.Web entity scalar columns (EF6 default plural table names).
  Run: sqlcmd -S <server> -d <database> -E -i Verify-ModelColumns.sql
  Exit: missing count printed; use -b and check ERRORLEVEL if you script CI.
*/
SET NOCOUNT ON;

DECLARE @Expected TABLE (
    Tbl SYSNAME NOT NULL,
    Col SYSNAME NOT NULL,
    PRIMARY KEY (Tbl, Col)
);

-- Applications
INSERT @Expected (Tbl, Col) VALUES
(N'Applications', N'Id'), (N'Applications', N'CompanyId'), (N'Applications', N'ApplicantId'), (N'Applications', N'PositionId'),
(N'Applications', N'Status'), (N'Applications', N'AppliedOn'), (N'Applications', N'ResumePath'), (N'Applications', N'WorkExperienceLevel'),
(N'Applications', N'Score'), (N'Applications', N'ScoreReason'), (N'Applications', N'CurrentStage'), (N'Applications', N'PendingQuestionnaireStage'),
(N'Applications', N'LastCompletedQuestionnaireStage'), (N'Applications', N'QuestionnaireInvitedOn'), (N'Applications', N'LastQuestionnaireScore'),
(N'Applications', N'CoverLetter'), (N'Applications', N'FailedCandidateEmailSentAt');

-- Positions
INSERT @Expected (Tbl, Col) VALUES
(N'Positions', N'Id'), (N'Positions', N'CompanyId'), (N'Positions', N'Title'), (N'Positions', N'Description'),
(N'Positions', N'Responsibilities'), (N'Positions', N'Qualifications'), (N'Positions', N'Currency'), (N'Positions', N'SalaryMin'),
(N'Positions', N'SalaryMax'), (N'Positions', N'PostedOn'), (N'Positions', N'IsOpen'), (N'Positions', N'ExpiryDate'),
(N'Positions', N'DepartmentId'), (N'Positions', N'Location'), (N'Positions', N'IsTechnical'), (N'Positions', N'PassMark'),
(N'Positions', N'PassMarksByStageJson'), (N'Positions', N'QuestionnaireStageCount');

-- Users
INSERT @Expected (Tbl, Col) VALUES
(N'Users', N'Id'), (N'Users', N'CompanyId'), (N'Users', N'FirstName'), (N'Users', N'LastName'), (N'Users', N'UserName'),
(N'Users', N'Email'), (N'Users', N'Role'), (N'Users', N'IsPanelist'), (N'Users', N'RoleDefinitionId'), (N'Users', N'Phone'),
(N'Users', N'DateOfBirth'), (N'Users', N'PasswordHash'), (N'Users', N'RequirePasswordChange'), (N'Users', N'LastPasswordChange'),
(N'Users', N'PasswordChangeExpiry'), (N'Users', N'AccessToken'), (N'Users', N'RefreshToken'), (N'Users', N'TokenExpiry'),
(N'Users', N'TwoFactorSecret'), (N'Users', N'IsTwoFactorEnabled'), (N'Users', N'MfaMethod'), (N'Users', N'TwoFactorCode'),
(N'Users', N'TwoFactorExpiry'), (N'Users', N'IsEmailVerified'), (N'Users', N'EmailVerificationCode'), (N'Users', N'EmailVerificationExpiry');

-- Applicants
INSERT @Expected (Tbl, Col) VALUES
(N'Applicants', N'Id'), (N'Applicants', N'CompanyId'), (N'Applicants', N'FullName'), (N'Applicants', N'Email'),
(N'Applicants', N'Phone'), (N'Applicants', N'DateOfBirth'), (N'Applicants', N'IsEmailVerified');

-- Companies
INSERT @Expected (Tbl, Col) VALUES
(N'Companies', N'Id'), (N'Companies', N'Name'), (N'Companies', N'Slug'), (N'Companies', N'AccessToken'), (N'Companies', N'IsActive'),
(N'Companies', N'LicenseExpiryDate'), (N'Companies', N'LogoPath'), (N'Companies', N'CreatedDate');

-- Departments
INSERT @Expected (Tbl, Col) VALUES
(N'Departments', N'Id'), (N'Departments', N'CompanyId'), (N'Departments', N'Name'), (N'Departments', N'Description');

-- PositionQuestions
INSERT @Expected (Tbl, Col) VALUES
(N'PositionQuestions', N'Id'), (N'PositionQuestions', N'PositionId'), (N'PositionQuestions', N'QuestionId'),
(N'PositionQuestions', N'Order'), (N'PositionQuestions', N'Weight'), (N'PositionQuestions', N'IsRequired'), (N'PositionQuestions', N'StageNumber');

-- Questions
INSERT @Expected (Tbl, Col) VALUES
(N'Questions', N'Id'), (N'Questions', N'CompanyId'), (N'Questions', N'Text'), (N'Questions', N'Type'),
(N'Questions', N'AllowMultipleChoices'), (N'Questions', N'IsActive');

-- ApplicationAnswers
INSERT @Expected (Tbl, Col) VALUES
(N'ApplicationAnswers', N'Id'), (N'ApplicationAnswers', N'ApplicationId'), (N'ApplicationAnswers', N'QuestionId'),
(N'ApplicationAnswers', N'StageNumber'), (N'ApplicationAnswers', N'AnswerText');

-- ApplicantProfiles
INSERT @Expected (Tbl, Col) VALUES
(N'ApplicantProfiles', N'Id'), (N'ApplicantProfiles', N'ApplicantId'), (N'ApplicantProfiles', N'Location'),
(N'ApplicantProfiles', N'TotalYearsExperience'), (N'ApplicantProfiles', N'RelevantYearsExperience'), (N'ApplicantProfiles', N'MostRecentCompany'),
(N'ApplicantProfiles', N'MostRecentTitle'), (N'ApplicantProfiles', N'MostRecentStartDate'), (N'ApplicantProfiles', N'MostRecentEndDate'),
(N'ApplicantProfiles', N'SecondMostRecentCompany'), (N'ApplicantProfiles', N'SecondMostRecentTitle'),
(N'ApplicantProfiles', N'SecondMostRecentStartDate'), (N'ApplicantProfiles', N'SecondMostRecentEndDate'),
(N'ApplicantProfiles', N'EmploymentType'), (N'ApplicantProfiles', N'Skills'), (N'ApplicantProfiles', N'Competencies'),
(N'ApplicantProfiles', N'EducationDegree'), (N'ApplicantProfiles', N'EducationInstitution'), (N'ApplicantProfiles', N'KeyAchievement'),
(N'ApplicantProfiles', N'Certifications'), (N'ApplicantProfiles', N'PortfolioUrl'), (N'ApplicantProfiles', N'WorkAuthorization'),
(N'ApplicantProfiles', N'NoticePeriod'), (N'ApplicantProfiles', N'CreatedOn'), (N'ApplicantProfiles', N'UpdatedOn');

-- CompanyHrCcEmails
INSERT @Expected (Tbl, Col) VALUES
(N'CompanyHrCcEmails', N'Id'), (N'CompanyHrCcEmails', N'CompanyId'), (N'CompanyHrCcEmails', N'Email'),
(N'CompanyHrCcEmails', N'Label'), (N'CompanyHrCcEmails', N'SortOrder'), (N'CompanyHrCcEmails', N'IsActive'), (N'CompanyHrCcEmails', N'CreatedDate');

-- RoleDefinitions / RolePermissions
INSERT @Expected (Tbl, Col) VALUES
(N'RoleDefinitions', N'Id'), (N'RoleDefinitions', N'CompanyId'), (N'RoleDefinitions', N'Name'), (N'RoleDefinitions', N'Description'),
(N'RoleDefinitions', N'CreatedByUserName'), (N'RoleDefinitions', N'CreatedDate'), (N'RoleDefinitions', N'IsActive');
INSERT @Expected (Tbl, Col) VALUES
(N'RolePermissions', N'Id'), (N'RolePermissions', N'RoleDefinitionId'), (N'RolePermissions', N'ModuleKey'), (N'RolePermissions', N'AccessLevel');

-- Interviews / Onboardings
INSERT @Expected (Tbl, Col) VALUES
(N'Interviews', N'Id'), (N'Interviews', N'CompanyId'), (N'Interviews', N'ApplicationId'), (N'Interviews', N'InterviewerId'),
(N'Interviews', N'ScheduledAt'), (N'Interviews', N'Mode'), (N'Interviews', N'Notes');
INSERT @Expected (Tbl, Col) VALUES
(N'Onboardings', N'Id'), (N'Onboardings', N'ApplicationId'), (N'Onboardings', N'StartDate'), (N'Onboardings', N'Tasks'), (N'Onboardings', N'Status');

-- QuestionOptions / PositionQuestionOptions
INSERT @Expected (Tbl, Col) VALUES
(N'QuestionOptions', N'Id'), (N'QuestionOptions', N'QuestionId'), (N'QuestionOptions', N'Text'), (N'QuestionOptions', N'Points');
INSERT @Expected (Tbl, Col) VALUES
(N'PositionQuestionOptions', N'Id'), (N'PositionQuestionOptions', N'PositionQuestionId'), (N'PositionQuestionOptions', N'QuestionOptionId'),
(N'PositionQuestionOptions', N'Points');

-- LoginAttempts / AuditLogs
INSERT @Expected (Tbl, Col) VALUES
(N'LoginAttempts', N'Id'), (N'LoginAttempts', N'CompanyId'), (N'LoginAttempts', N'Username'), (N'LoginAttempts', N'IPAddress'),
(N'LoginAttempts', N'AttemptTime'), (N'LoginAttempts', N'WasSuccessful'), (N'LoginAttempts', N'FailureReason');
INSERT @Expected (Tbl, Col) VALUES
(N'AuditLogs', N'Id'), (N'AuditLogs', N'CompanyId'), (N'AuditLogs', N'Username'), (N'AuditLogs', N'Action'), (N'AuditLogs', N'Controller'),
(N'AuditLogs', N'EntityId'), (N'AuditLogs', N'OldValues'), (N'AuditLogs', N'NewValues'), (N'AuditLogs', N'IPAddress'),
(N'AuditLogs', N'Timestamp'), (N'AuditLogs', N'UserAgent'), (N'AuditLogs', N'WasSuccessful'), (N'AuditLogs', N'ErrorMessage');

-- Reports / PasswordResets / LicenseTransactions / ImpersonationRequests
INSERT @Expected (Tbl, Col) VALUES
(N'Reports', N'Id'), (N'Reports', N'Name'), (N'Reports', N'Type'), (N'Reports', N'Description'), (N'Reports', N'CreatedDate'),
(N'Reports', N'GeneratedDate'), (N'Reports', N'GeneratedBy'), (N'Reports', N'FilePath'), (N'Reports', N'IsActive'), (N'Reports', N'Parameters');
INSERT @Expected (Tbl, Col) VALUES
(N'PasswordResets', N'Id'), (N'PasswordResets', N'UserId'), (N'PasswordResets', N'Token'), (N'PasswordResets', N'ExpiryDate'),
(N'PasswordResets', N'IsUsed'), (N'PasswordResets', N'CreatedDate'), (N'PasswordResets', N'RequestingIP'), (N'PasswordResets', N'CompletedIP');
INSERT @Expected (Tbl, Col) VALUES
(N'LicenseTransactions', N'Id'), (N'LicenseTransactions', N'CompanyId'), (N'LicenseTransactions', N'ExecutedBy'),
(N'LicenseTransactions', N'TransactionDate'), (N'LicenseTransactions', N'PreviousExpiry'), (N'LicenseTransactions', N'NewExpiry'),
(N'LicenseTransactions', N'ExtendedByValue'), (N'LicenseTransactions', N'ExtendedByUnit'), (N'LicenseTransactions', N'Notes');
INSERT @Expected (Tbl, Col) VALUES
(N'ImpersonationRequests', N'Id'), (N'ImpersonationRequests', N'CompanyId'), (N'ImpersonationRequests', N'RequestedBy'),
(N'ImpersonationRequests', N'RequestedFrom'), (N'ImpersonationRequests', N'RequestDate'), (N'ImpersonationRequests', N'Status'),
(N'ImpersonationRequests', N'Reason'), (N'ImpersonationRequests', N'DecisionDate'), (N'ImpersonationRequests', N'AdminNotes'),
(N'ImpersonationRequests', N'ExpiryDate');

-- SystemSettings / TemporaryCredentials
INSERT @Expected (Tbl, Col) VALUES
(N'SystemSettings', N'SettingKey'), (N'SystemSettings', N'SettingValue'), (N'SystemSettings', N'Description'), (N'SystemSettings', N'IsEncrypted');
INSERT @Expected (Tbl, Col) VALUES
(N'TemporaryCredentials', N'Id'), (N'TemporaryCredentials', N'Token'), (N'TemporaryCredentials', N'EncryptedData'),
(N'TemporaryCredentials', N'ExpiryDate'), (N'TemporaryCredentials', N'IsUsed'), (N'TemporaryCredentials', N'CreatedDate'),
(N'TemporaryCredentials', N'CredentialType');

-- Tables that should exist
DECLARE @MissingTables TABLE (Tbl SYSNAME NOT NULL);
INSERT @MissingTables (Tbl)
SELECT DISTINCT e.Tbl
FROM @Expected e
WHERE NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'dbo' AND t.name = e.Tbl
);

IF EXISTS (SELECT 1 FROM @MissingTables)
BEGIN
    PRINT N'--- Missing tables (dbo) ---';
    SELECT Tbl AS MissingTable FROM @MissingTables ORDER BY Tbl;
END
ELSE
    PRINT N'--- All expected dbo tables exist ---';

-- Missing columns (table exists)
DECLARE @MissingCols TABLE (Tbl SYSNAME NOT NULL, Col SYSNAME NOT NULL);
INSERT @MissingCols (Tbl, Col)
SELECT e.Tbl, e.Col
FROM @Expected e
WHERE EXISTS (
    SELECT 1 FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'dbo' AND t.name = e.Tbl
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'dbo' AND t.name = e.Tbl AND c.name = e.Col
);

DECLARE @cnt INT = (SELECT COUNT(*) FROM @MissingCols);
PRINT N'--- Missing column count: ' + CAST(@cnt AS NVARCHAR(20)) + N' ---';

IF @cnt > 0
    SELECT Tbl AS [Table], Col AS MissingColumn FROM @MissingCols ORDER BY Tbl, Col;
ELSE
    PRINT N'OK: All expected scalar columns are present for HR.Web entity set.';
