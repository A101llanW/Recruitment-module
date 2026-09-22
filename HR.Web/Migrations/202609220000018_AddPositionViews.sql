-- Candidate position view tags and successful login counter (idempotent).

IF COL_LENGTH(N'dbo.Users', N'SuccessfulLoginCount') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD SuccessfulLoginCount INT NOT NULL
        CONSTRAINT DF_Users_SuccessfulLoginCount DEFAULT (0);
    PRINT 'Added dbo.Users.SuccessfulLoginCount';
END
ELSE
BEGIN
    PRINT 'dbo.Users.SuccessfulLoginCount already exists.';
END

IF OBJECT_ID(N'dbo.PositionViews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PositionViews (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_PositionViews PRIMARY KEY,
        UserId INT NOT NULL,
        PositionId INT NOT NULL,
        ViewedAtUtc DATETIME2(0) NOT NULL,
        LoginCountAtView INT NOT NULL,
        IsOpenAtView BIT NOT NULL,
        CONSTRAINT FK_PositionViews_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_PositionViews_Positions FOREIGN KEY (PositionId) REFERENCES dbo.Positions (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_PositionViews_User_Position
        ON dbo.PositionViews (UserId, PositionId);
    PRINT 'Created dbo.PositionViews';
END
ELSE
BEGIN
    PRINT 'dbo.PositionViews already exists.';
END
