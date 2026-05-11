-- Add staged questionnaire support (idempotent)

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PositionQuestions')
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.PositionQuestions')
          AND name = 'StageNumber'
    )
    BEGIN
        ALTER TABLE dbo.PositionQuestions
            ADD StageNumber INT NOT NULL CONSTRAINT DF_PositionQuestions_StageNumber DEFAULT(1);
        PRINT 'Added PositionQuestions.StageNumber';
    END
END

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Applications')
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Applications')
          AND name = 'CurrentStage'
    )
    BEGIN
        ALTER TABLE dbo.Applications
            ADD CurrentStage INT NOT NULL CONSTRAINT DF_Applications_CurrentStage DEFAULT(1);
        PRINT 'Added Applications.CurrentStage';
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Applications')
          AND name = 'IsStageTwoInvited'
    )
    BEGIN
        ALTER TABLE dbo.Applications
            ADD IsStageTwoInvited BIT NOT NULL CONSTRAINT DF_Applications_IsStageTwoInvited DEFAULT(0);
        PRINT 'Added Applications.IsStageTwoInvited';
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Applications')
          AND name = 'StageTwoInvitedOn'
    )
    BEGIN
        ALTER TABLE dbo.Applications ADD StageTwoInvitedOn DATETIME NULL;
        PRINT 'Added Applications.StageTwoInvitedOn';
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Applications')
          AND name = 'StageTwoCompletedOn'
    )
    BEGIN
        ALTER TABLE dbo.Applications ADD StageTwoCompletedOn DATETIME NULL;
        PRINT 'Added Applications.StageTwoCompletedOn';
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Applications')
          AND name = 'StageTwoScore'
    )
    BEGIN
        ALTER TABLE dbo.Applications ADD StageTwoScore DECIMAL(18,2) NULL;
        PRINT 'Added Applications.StageTwoScore';
    END
END

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ApplicationAnswers')
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.ApplicationAnswers')
          AND name = 'StageNumber'
    )
    BEGIN
        ALTER TABLE dbo.ApplicationAnswers
            ADD StageNumber INT NOT NULL CONSTRAINT DF_ApplicationAnswers_StageNumber DEFAULT(1);
        PRINT 'Added ApplicationAnswers.StageNumber';
    END
END
