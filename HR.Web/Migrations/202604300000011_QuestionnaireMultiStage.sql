-- Questionnaire multi-stage (idempotent for SQL Server)

IF COL_LENGTH('dbo.Positions', 'QuestionnaireStageCount') IS NULL
BEGIN
    ALTER TABLE dbo.Positions ADD QuestionnaireStageCount INT NOT NULL CONSTRAINT DF_Positions_QuestionnaireStageCount DEFAULT(1);
    PRINT 'Added Positions.QuestionnaireStageCount';
END
GO

IF COL_LENGTH('dbo.Positions', 'HasSecondaryStage') IS NOT NULL
BEGIN
    UPDATE dbo.Positions SET QuestionnaireStageCount = CASE WHEN HasSecondaryStage = 1 THEN 2 ELSE 1 END;
    DECLARE @dfPos sysname;
    SELECT @dfPos = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.Positions') AND c.name = 'HasSecondaryStage';
    IF @dfPos IS NOT NULL
    BEGIN
        DECLARE @sqlPos nvarchar(512) = N'ALTER TABLE dbo.Positions DROP CONSTRAINT ' + QUOTENAME(@dfPos);
        EXEC sp_executesql @sqlPos;
    END
    ALTER TABLE dbo.Positions DROP COLUMN HasSecondaryStage;
    PRINT 'Migrated HasSecondaryStage to QuestionnaireStageCount and dropped HasSecondaryStage';
END
GO

IF COL_LENGTH('dbo.Applications', 'PendingQuestionnaireStage') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD PendingQuestionnaireStage INT NULL;
    PRINT 'Added Applications.PendingQuestionnaireStage';
END
GO

IF COL_LENGTH('dbo.Applications', 'LastCompletedQuestionnaireStage') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD LastCompletedQuestionnaireStage INT NOT NULL CONSTRAINT DF_Applications_LastCompletedQuestionnaireStage DEFAULT(0);
    PRINT 'Added Applications.LastCompletedQuestionnaireStage';
END
GO

IF COL_LENGTH('dbo.Applications', 'QuestionnaireInvitedOn') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD QuestionnaireInvitedOn DATETIME NULL;
    PRINT 'Added Applications.QuestionnaireInvitedOn';
END
GO

IF COL_LENGTH('dbo.Applications', 'LastQuestionnaireScore') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD LastQuestionnaireScore DECIMAL(18,2) NULL;
    PRINT 'Added Applications.LastQuestionnaireScore';
END
GO

IF COL_LENGTH('dbo.Applications', 'IsStageTwoInvited') IS NOT NULL
BEGIN
    UPDATE dbo.Applications SET
        LastCompletedQuestionnaireStage = CASE
            WHEN StageTwoCompletedOn IS NOT NULL THEN 2
            WHEN IsStageTwoInvited = 1 THEN 1
            ELSE 0 END,
        PendingQuestionnaireStage = CASE
            WHEN IsStageTwoInvited = 1 AND StageTwoCompletedOn IS NULL THEN 2
            ELSE NULL END,
        QuestionnaireInvitedOn = StageTwoInvitedOn,
        LastQuestionnaireScore = StageTwoScore;

    DECLARE @dfApp sysname;
    SELECT @dfApp = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.Applications') AND c.name = 'IsStageTwoInvited';
    IF @dfApp IS NOT NULL
    BEGIN
        DECLARE @sqlApp nvarchar(512) = N'ALTER TABLE dbo.Applications DROP CONSTRAINT ' + QUOTENAME(@dfApp);
        EXEC sp_executesql @sqlApp;
    END

    ALTER TABLE dbo.Applications DROP COLUMN IsStageTwoInvited;
    ALTER TABLE dbo.Applications DROP COLUMN StageTwoInvitedOn;
    ALTER TABLE dbo.Applications DROP COLUMN StageTwoCompletedOn;
    ALTER TABLE dbo.Applications DROP COLUMN StageTwoScore;
    PRINT 'Migrated application stage columns';
END
GO
