-- Update Users table for expanded MFA options
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'MfaMethod')
BEGIN
    ALTER TABLE Users ADD MfaMethod NVARCHAR(50) NULL;
    PRINT 'Added MfaMethod column to Users.';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'TwoFactorCode')
BEGIN
    ALTER TABLE Users ADD TwoFactorCode NVARCHAR(10) NULL;
    PRINT 'Added TwoFactorCode column to Users.';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'TwoFactorExpiry')
BEGIN
    ALTER TABLE Users ADD TwoFactorExpiry DATETIME NULL;
    PRINT 'Added TwoFactorExpiry column to Users.';
END
GO

-- Default existing SuperAdmins/Admins to 'App' method if they had it enabled
UPDATE Users SET MfaMethod = 'App' WHERE IsTwoFactorEnabled = 1 AND MfaMethod IS NULL;
GO
