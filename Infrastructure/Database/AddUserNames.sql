-- SQL Script to add FirstName and LastName columns to the Users table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'FirstName')
BEGIN
    ALTER TABLE [dbo].[Users] ADD [FirstName] NVARCHAR(100) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'LastName')
BEGIN
    ALTER TABLE [dbo].[Users] ADD [LastName] NVARCHAR(100) NULL;
END
GO

-- Update existing users with placeholder names based on username if null
UPDATE [dbo].[Users] 
SET [FirstName] = [UserName], [LastName] = 'User'
WHERE [FirstName] IS NULL;
GO
