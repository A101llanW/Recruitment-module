-- =====================================================================
-- AUDIT LOG MAINTENANCE
-- Run this script manually or schedule it as a SQL Server Agent Job
-- to prevent unbounded growth of audit and security tables.
-- Recommended schedule: Daily at 02:00
-- =====================================================================

-- ── Retention Policy ─────────────────────────────────────────────────
DECLARE @AuditRetentionDays     INT = 90   -- Keep 90 days of audit logs
DECLARE @LoginRetentionDays     INT = 30   -- Keep 30 days of login attempts
DECLARE @PasswordResetRetDays   INT = 7    -- Keep 7 days of password reset records

-- ── 1. Purge old Audit Logs ──────────────────────────────────────────
DELETE FROM AuditLogs
WHERE Timestamp < DATEADD(DAY, -@AuditRetentionDays, GETDATE())

PRINT CONCAT('AuditLogs purged: ', @@ROWCOUNT, ' rows older than ', @AuditRetentionDays, ' days removed.')

-- ── 2. Purge old Login Attempts ──────────────────────────────────────
DELETE FROM LoginAttempts
WHERE AttemptTime < DATEADD(DAY, -@LoginRetentionDays, GETDATE())

PRINT CONCAT('LoginAttempts purged: ', @@ROWCOUNT, ' rows older than ', @LoginRetentionDays, ' days removed.')

-- ── 3. Purge expired/used Password Resets ────────────────────────────
DELETE FROM PasswordResets
WHERE (IsUsed = 1 OR ExpiryDate < GETDATE())
  AND CreatedDate < DATEADD(DAY, -@PasswordResetRetDays, GETDATE())

PRINT CONCAT('PasswordResets purged: ', @@ROWCOUNT, ' expired/used records removed.')

-- ── 4. Add new IP-tracking columns if not already present ─────────────
-- (Run this once to support the new PasswordReset IP binding feature)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PasswordResets' AND COLUMN_NAME = 'RequestingIP'
)
BEGIN
    ALTER TABLE PasswordResets ADD RequestingIP NVARCHAR(100) NULL
    PRINT 'Added RequestingIP column to PasswordResets.'
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PasswordResets' AND COLUMN_NAME = 'CompletedIP'
)
BEGIN
    ALTER TABLE PasswordResets ADD CompletedIP NVARCHAR(100) NULL
    PRINT 'Added CompletedIP column to PasswordResets.'
END

-- ── Done ──────────────────────────────────────────────────────────────
PRINT 'Maintenance complete.'
