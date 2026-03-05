-- =============================================
-- MFA SCHEMA UPDATE
-- Adds support for Two-Factor Authentication
-- =============================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'TwoFactorSecret'
)
BEGIN
    ALTER TABLE Users ADD TwoFactorSecret NVARCHAR(256) NULL;
    PRINT 'Added TwoFactorSecret column to Users.';
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'IsTwoFactorEnabled'
)
BEGIN
    ALTER TABLE Users ADD IsTwoFactorEnabled BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsTwoFactorEnabled column to Users.';
END
