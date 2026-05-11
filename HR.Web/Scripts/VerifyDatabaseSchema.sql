/*
  HR.Web — compare SQL Server schema to current EF6 models (column names only).
  Run in SSMS (or sqlcmd) against the same database as connection string "HrContext".

  If this query returns rows, those columns are missing (or the table is missing).
  After fixing schema, re-run to confirm empty result.

  Apply schema (in order):
  1. Package Manager Console:  Update-Database  (Default project: HR.Web, Startup project: HR.Web)
     — uses explicit migrations in HR.Web (AutomaticMigrationsEnabled = false).
  2. Run idempotent SQL under Migrations\ that are not covered by a .cs migration, e.g.:
        Migrations\202605050000013_AddCompanyHrCcEmails.sql  (creates dbo.CompanyHrCcEmails)
  3. Optional: run any *.sql in Migrations\ for your environment (they are idempotent where noted).

  Common causes of "Invalid column name" / EntityCommandExecutionException:
  - dbo.Positions missing PassMarksByStageJson, QuestionnaireStageCount, PassMark, etc.
  - dbo.Users missing IsPanelist
*/

SET NOCOUNT ON;

DECLARE @Missing TABLE
(
    Severity    VARCHAR(10) NOT NULL, -- ERROR if table missing, WARN if column missing
    TableName   SYSNAME NOT NULL,
    ColumnName  SYSNAME NULL -- NULL when entire table is missing
);

IF OBJECT_ID(N'dbo.Positions', N'U') IS NULL
    INSERT INTO @Missing (Severity, TableName, ColumnName) VALUES ('ERROR', N'dbo.Positions', NULL);
ELSE
BEGIN
    INSERT INTO @Missing (Severity, TableName, ColumnName)
    SELECT 'WARN', N'dbo.Positions', v.ColumnName
    FROM (VALUES
        (N'Id'), (N'CompanyId'), (N'Title'), (N'Description'), (N'Responsibilities'), (N'Qualifications'),
        (N'Currency'), (N'SalaryMin'), (N'SalaryMax'), (N'PostedOn'), (N'IsOpen'), (N'ExpiryDate'),
        (N'DepartmentId'), (N'Location'), (N'IsTechnical'), (N'PassMark'), (N'PassMarksByStageJson'),
        (N'QuestionnaireStageCount')
    ) AS v(ColumnName)
    WHERE NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_SCHEMA = N'dbo' AND c.TABLE_NAME = N'Positions' AND c.COLUMN_NAME = v.ColumnName);
END

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    INSERT INTO @Missing (Severity, TableName, ColumnName) VALUES ('ERROR', N'dbo.Users', NULL);
ELSE
BEGIN
    INSERT INTO @Missing (Severity, TableName, ColumnName)
    SELECT 'WARN', N'dbo.Users', v.ColumnName
    FROM (VALUES
        (N'Id'), (N'CompanyId'), (N'FirstName'), (N'LastName'), (N'UserName'), (N'Email'), (N'Role'),
        (N'IsPanelist'), (N'RoleDefinitionId'), (N'Phone'), (N'DateOfBirth'), (N'PasswordHash'),
        (N'RequirePasswordChange'), (N'LastPasswordChange'), (N'PasswordChangeExpiry'),
        (N'AccessToken'), (N'RefreshToken'), (N'TokenExpiry'), (N'TwoFactorSecret'), (N'IsTwoFactorEnabled'),
        (N'MfaMethod'), (N'TwoFactorCode'), (N'TwoFactorExpiry'), (N'IsEmailVerified'),
        (N'EmailVerificationCode'), (N'EmailVerificationExpiry')
    ) AS v(ColumnName)
    WHERE NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_SCHEMA = N'dbo' AND c.TABLE_NAME = N'Users' AND c.COLUMN_NAME = v.ColumnName);
END

IF OBJECT_ID(N'dbo.Applications', N'U') IS NULL
    INSERT INTO @Missing (Severity, TableName, ColumnName) VALUES ('ERROR', N'dbo.Applications', NULL);
ELSE
BEGIN
    INSERT INTO @Missing (Severity, TableName, ColumnName)
    SELECT 'WARN', N'dbo.Applications', v.ColumnName
    FROM (VALUES
        (N'Id'), (N'CompanyId'), (N'ApplicantId'), (N'PositionId'), (N'Status'), (N'AppliedOn'),
        (N'ResumePath'), (N'WorkExperienceLevel'), (N'Score'), (N'ScoreReason'), (N'CurrentStage'),
        (N'PendingQuestionnaireStage'), (N'LastCompletedQuestionnaireStage'), (N'QuestionnaireInvitedOn'),
        (N'LastQuestionnaireScore'), (N'CoverLetter')
    ) AS v(ColumnName)
    WHERE NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_SCHEMA = N'dbo' AND c.TABLE_NAME = N'Applications' AND c.COLUMN_NAME = v.ColumnName);
END

IF OBJECT_ID(N'dbo.PositionQuestions', N'U') IS NULL
    INSERT INTO @Missing (Severity, TableName, ColumnName) VALUES ('ERROR', N'dbo.PositionQuestions', NULL);
ELSE
BEGIN
    INSERT INTO @Missing (Severity, TableName, ColumnName)
    SELECT 'WARN', N'dbo.PositionQuestions', v.ColumnName
    FROM (VALUES
        (N'Id'), (N'PositionId'), (N'QuestionId'), (N'Order'), (N'Weight'), (N'IsRequired'), (N'StageNumber')
    ) AS v(ColumnName)
    WHERE NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_SCHEMA = N'dbo' AND c.TABLE_NAME = N'PositionQuestions' AND c.COLUMN_NAME = v.ColumnName);
END

IF OBJECT_ID(N'dbo.Questions', N'U') IS NULL
    INSERT INTO @Missing (Severity, TableName, ColumnName) VALUES ('ERROR', N'dbo.Questions', NULL);
ELSE
BEGIN
    INSERT INTO @Missing (Severity, TableName, ColumnName)
    SELECT 'WARN', N'dbo.Questions', v.ColumnName
    FROM (VALUES
        (N'Id'), (N'CompanyId'), (N'Text'), (N'Type'), (N'AllowMultipleChoices'), (N'IsActive')
    ) AS v(ColumnName)
    WHERE NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_SCHEMA = N'dbo' AND c.TABLE_NAME = N'Questions' AND c.COLUMN_NAME = v.ColumnName);
END

IF OBJECT_ID(N'dbo.ApplicationAnswers', N'U') IS NULL
    INSERT INTO @Missing (Severity, TableName, ColumnName) VALUES ('ERROR', N'dbo.ApplicationAnswers', NULL);
ELSE
BEGIN
    INSERT INTO @Missing (Severity, TableName, ColumnName)
    SELECT 'WARN', N'dbo.ApplicationAnswers', v.ColumnName
    FROM (VALUES
        (N'Id'), (N'ApplicationId'), (N'QuestionId'), (N'StageNumber'), (N'AnswerText')
    ) AS v(ColumnName)
    WHERE NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_SCHEMA = N'dbo' AND c.TABLE_NAME = N'ApplicationAnswers' AND c.COLUMN_NAME = v.ColumnName);
END

IF OBJECT_ID(N'dbo.Companies', N'U') IS NULL
    INSERT INTO @Missing (Severity, TableName, ColumnName) VALUES ('ERROR', N'dbo.Companies', NULL);
ELSE
BEGIN
    INSERT INTO @Missing (Severity, TableName, ColumnName)
    SELECT 'WARN', N'dbo.Companies', v.ColumnName
    FROM (VALUES
        (N'Id'), (N'Name'), (N'Slug'), (N'AccessToken'), (N'IsActive'), (N'LicenseExpiryDate'),
        (N'LogoPath'), (N'CreatedDate')
    ) AS v(ColumnName)
    WHERE NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_SCHEMA = N'dbo' AND c.TABLE_NAME = N'Companies' AND c.COLUMN_NAME = v.ColumnName);
END

IF OBJECT_ID(N'dbo.CompanyHrCcEmails', N'U') IS NULL
    INSERT INTO @Missing (Severity, TableName, ColumnName) VALUES ('ERROR', N'dbo.CompanyHrCcEmails', NULL);
ELSE
BEGIN
    INSERT INTO @Missing (Severity, TableName, ColumnName)
    SELECT 'WARN', N'dbo.CompanyHrCcEmails', v.ColumnName
    FROM (VALUES
        (N'Id'), (N'CompanyId'), (N'Email'), (N'Label'), (N'SortOrder'), (N'IsActive'), (N'CreatedDate')
    ) AS v(ColumnName)
    WHERE NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_SCHEMA = N'dbo' AND c.TABLE_NAME = N'CompanyHrCcEmails' AND c.COLUMN_NAME = v.ColumnName);
END

SELECT Severity, TableName, ColumnName
FROM @Missing
ORDER BY Severity DESC, TableName,
    CASE WHEN ColumnName IS NULL THEN 0 ELSE 1 END,
    ColumnName;

IF @@ROWCOUNT = 0
    PRINT 'VerifyDatabaseSchema: no missing tables/columns detected for checked objects.';
