-- Add password change columns to Users table
USE HR_Local;

-- Add new columns if they don't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'RequirePasswordChange')
BEGIN
    ALTER TABLE Users ADD RequirePasswordChange BIT NOT NULL DEFAULT 0;
    PRINT 'Added RequirePasswordChange column';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LastPasswordChange')
BEGIN
    ALTER TABLE Users ADD LastPasswordChange DATETIME NULL;
    PRINT 'Added LastPasswordChange column';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'PasswordChangeExpiry')
BEGIN
    ALTER TABLE Users ADD PasswordChangeExpiry DATETIME NULL;
    PRINT 'Added PasswordChangeExpiry column';
END

-- Update existing users with weak passwords to require password change
UPDATE Users 
SET RequirePasswordChange = 1, 
    PasswordChangeExpiry = DATEADD(day, 7, GETDATE())
WHERE RequirePasswordChange = 0 
AND (
    PasswordHash LIKE '%10000%' OR -- Old iteration count
    LEN(PasswordHash) < 50 OR -- Short hash (likely weak)
    LastPasswordChange IS NULL -- No password change record
);

PRINT 'Updated existing users with weak passwords to require password change';
PRINT 'Migration completed successfully!';
