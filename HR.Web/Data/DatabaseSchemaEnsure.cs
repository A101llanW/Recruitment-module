using System;
using System.Data.SqlClient;

namespace HR.Web.Data
{
    /// <summary>
    /// Idempotent SQL Server column adds for features shipped after the DB was created.
    /// Migrations are not auto-applied (<see cref="System.Data.Entity.Database.SetInitializer"/> is null); this avoids runtime "Invalid column name" when EF maps new properties.
    /// </summary>
    public static class DatabaseSchemaEnsure
    {
        public static void ApplyOptionalColumns(HrContext db)
        {
            if (db == null)
            {
                return;
            }

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF OBJECT_ID(N'dbo.CompanyHrCcEmails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyHrCcEmails (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_CompanyHrCcEmails PRIMARY KEY,
        CompanyId INT NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        Label NVARCHAR(150) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_CompanyHrCcEmails_SortOrder DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_CompanyHrCcEmails_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_CompanyHrCcEmails_CreatedDate DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CompanyHrCcEmails_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_CompanyHrCcEmails_CompanyId ON dbo.CompanyHrCcEmails (CompanyId);
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applications', N'FailedCandidateEmailSentAt') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD FailedCandidateEmailSentAt DATETIME NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applications', N'CoverLetter') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD CoverLetter NVARCHAR(MAX) NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Positions', N'PassMarksByStageJson') IS NULL
BEGIN
    ALTER TABLE dbo.Positions ADD PassMarksByStageJson NVARCHAR(4000) NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Positions', N'QuestionnaireStageCount') IS NULL
BEGIN
    ALTER TABLE dbo.Positions ADD QuestionnaireStageCount INT NOT NULL CONSTRAINT DF_Positions_QuestionnaireStageCount DEFAULT (1);
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.PositionQuestions', N'StageNumber') IS NULL
BEGIN
    ALTER TABLE dbo.PositionQuestions ADD StageNumber INT NOT NULL CONSTRAINT DF_PositionQuestions_StageNumber DEFAULT (1);
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.ApplicationAnswers', N'StageNumber') IS NULL
BEGIN
    ALTER TABLE dbo.ApplicationAnswers ADD StageNumber INT NOT NULL CONSTRAINT DF_ApplicationAnswers_StageNumber DEFAULT (1);
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applications', N'CurrentStage') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD CurrentStage INT NOT NULL CONSTRAINT DF_Applications_CurrentStage DEFAULT (1);
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applications', N'LastCompletedQuestionnaireStage') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD LastCompletedQuestionnaireStage INT NOT NULL CONSTRAINT DF_Applications_LastCompletedQuestionnaireStage DEFAULT (0);
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applications', N'PendingQuestionnaireStage') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD PendingQuestionnaireStage INT NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applications', N'QuestionnaireInvitedOn') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD QuestionnaireInvitedOn DATETIME NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applications', N'LastQuestionnaireScore') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD LastQuestionnaireScore DECIMAL(18,2) NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Users', N'PrivacyAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD PrivacyAcceptedAt DATETIME NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Users', N'TermsAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TermsAcceptedAt DATETIME NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Users', N'PrivacyVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD PrivacyVersion NVARCHAR(20) NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Users', N'TermsVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TermsVersion NVARCHAR(20) NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applicants', N'PrivacyAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Applicants ADD PrivacyAcceptedAt DATETIME NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applicants', N'TermsAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Applicants ADD TermsAcceptedAt DATETIME NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applicants', N'PrivacyVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Applicants ADD PrivacyVersion NVARCHAR(20) NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF COL_LENGTH(N'dbo.Applicants', N'TermsVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Applicants ADD TermsVersion NVARCHAR(20) NULL;
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF OBJECT_ID(N'dbo.QuestionnaireTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuestionnaireTemplates (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_QuestionnaireTemplates PRIMARY KEY,
        CompanyId INT NULL,
        Name NVARCHAR(150) NOT NULL,
        Description NVARCHAR(500) NULL,
        StageCount INT NOT NULL CONSTRAINT DF_QuestionnaireTemplates_StageCount DEFAULT (1),
        IsActive BIT NOT NULL CONSTRAINT DF_QuestionnaireTemplates_IsActive DEFAULT (1),
        CreatedOn DATETIME NOT NULL CONSTRAINT DF_QuestionnaireTemplates_CreatedOn DEFAULT (GETUTCDATE()),
        UpdatedOn DATETIME NULL,
        CONSTRAINT FK_QuestionnaireTemplates_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id)
    );
    CREATE NONCLUSTERED INDEX IX_QuestionnaireTemplates_CompanyId ON dbo.QuestionnaireTemplates (CompanyId);
END"));

            TryExecute(db, ctx => ctx.Database.ExecuteSqlCommand(@"
IF OBJECT_ID(N'dbo.QuestionnaireTemplateQuestions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuestionnaireTemplateQuestions (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_QuestionnaireTemplateQuestions PRIMARY KEY,
        TemplateId INT NOT NULL,
        QuestionId INT NOT NULL,
        [Order] INT NOT NULL,
        Weight DECIMAL(18, 2) NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_QuestionnaireTemplateQuestions_IsRequired DEFAULT (1),
        StageNumber INT NOT NULL CONSTRAINT DF_QuestionnaireTemplateQuestions_StageNumber DEFAULT (1),
        CONSTRAINT FK_QuestionnaireTemplateQuestions_Templates FOREIGN KEY (TemplateId) REFERENCES dbo.QuestionnaireTemplates (Id) ON DELETE CASCADE,
        CONSTRAINT FK_QuestionnaireTemplateQuestions_Questions FOREIGN KEY (QuestionId) REFERENCES dbo.Questions (Id)
    );
    CREATE NONCLUSTERED INDEX IX_QuestionnaireTemplateQuestions_TemplateId ON dbo.QuestionnaireTemplateQuestions (TemplateId);
    CREATE NONCLUSTERED INDEX IX_QuestionnaireTemplateQuestions_QuestionId ON dbo.QuestionnaireTemplateQuestions (QuestionId);
END"));
        }

        private static void TryExecute(HrContext db, Action<HrContext> execute)
        {
            try
            {
                execute(db);
            }
            catch (SqlException ex)
            {
                System.Diagnostics.Debug.WriteLine("[DatabaseSchemaEnsure] " + ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DatabaseSchemaEnsure] " + ex.Message);
            }
        }
    }
}
