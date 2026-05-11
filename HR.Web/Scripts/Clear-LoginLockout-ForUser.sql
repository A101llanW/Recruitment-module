/*
  Removes failed login rows that trigger account lockout (5 failures in 30 minutes by default).

  Use when a SuperAdmin / global account is locked and you cannot reach Admin > Unlock.

  1. Set @UserName to the exact login name (case-insensitive match).
  2. Run against your HR database (same DB as connectionStrings in Web.config).

  Optional: set @GlobalOnly = 1 to delete only rows where CompanyId IS NULL (typical for
  global SuperAdmin after recent app changes). Set @GlobalOnly = 0 to clear all failed
  attempts for that username in every company scope.
*/

DECLARE @UserName NVARCHAR(100) = N'SuperAdmin'; -- <-- change this
DECLARE @GlobalOnly BIT = 0;                      -- 1 = CompanyId IS NULL only; 0 = all scopes

DELETE la
FROM dbo.LoginAttempts AS la
WHERE la.WasSuccessful = 0
  AND la.Username IS NOT NULL
  AND LOWER(la.Username) = LOWER(@UserName)
  AND (@GlobalOnly = 0 OR la.CompanyId IS NULL);

SELECT @@ROWCOUNT AS FailedAttemptsRemoved;

/*
  --- Optional: unlock every global Admin / SuperAdmin (CompanyId IS NULL on Users) in one shot ---

DELETE la
FROM dbo.LoginAttempts AS la
WHERE la.WasSuccessful = 0
  AND la.CompanyId IS NULL
  AND EXISTS (
      SELECT 1
      FROM dbo.Users AS u
      WHERE LOWER(u.UserName) = LOWER(la.Username)
        AND u.CompanyId IS NULL
        AND LOWER(LTRIM(RTRIM(ISNULL(u.Role, N'')))) IN (N'admin', N'superadmin')
  );

SELECT @@ROWCOUNT AS FailedAttemptsRemoved_GlobalOperators;
*/
