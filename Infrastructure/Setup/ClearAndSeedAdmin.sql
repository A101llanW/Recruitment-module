USE HR_Local;
GO

-- Disable constraints to allow clearing tables
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'

-- Clear all data from all HR related tables
DELETE FROM PasswordResets;
DELETE FROM AuditLogs;
DELETE FROM LoginAttempts;
DELETE FROM ImpersonationRequests;
DELETE FROM ApplicationAnswers;
DELETE FROM Onboardings;
DELETE FROM Interviews;
DELETE FROM Applications;
DELETE FROM Applicants;
DELETE FROM PositionQuestionOptions;
DELETE FROM PositionQuestions;
DELETE FROM QuestionOptions;
DELETE FROM Questions;
DELETE FROM Positions;
DELETE FROM Departments;
DELETE FROM Users;
DELETE FROM LicenseTransactions;
DELETE FROM Reports;
DELETE FROM Companies;

-- Re-enable constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'

-- Reset Identity columns
EXEC sp_MSforeachtable 'DBCC CHECKIDENT ("?", RESEED, 0)'

-- Insert single SuperAdmin account
-- Password: Admin@123 (Hash generated for Iterations=100000)
INSERT INTO Users (FirstName, LastName, UserName, Email, Role, CompanyId, PasswordHash, IsTwoFactorEnabled, RequirePasswordChange)
VALUES ('System', 'Administrator', 'admin', 'admin@hrsystem.com', 'SuperAdmin', NULL, '100000.66/ziKCror0LSOo7o1mXKg==.HEwwkXvTGHxdP4o/k9ZzQ/VaicmZpbZOJkEQtEM843k=', 0, 0);

PRINT 'Database cleared and single SuperAdmin account (admin/Admin@123) created.';
GO
