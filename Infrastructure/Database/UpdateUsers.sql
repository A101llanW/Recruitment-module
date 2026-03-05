USE HR_Local;
GO

-- 1. Update/Ensure 'admin' SuperAdmin
IF EXISTS (SELECT * FROM Users WHERE UserName = 'admin')
BEGIN
    UPDATE Users 
    SET PasswordHash = '100000.66/ziKCror0LSOo7o1mXKg==.HEwwkXvTGHxdP4o/k9ZzQ/VaicmZpbZOJkEQtEM843k=',
        Role = 'SuperAdmin',
        CompanyId = NULL
    WHERE UserName = 'admin';
END
ELSE
BEGIN
    INSERT INTO Users (UserName, PasswordHash, Role, CompanyId, FirstName, LastName)
    VALUES ('admin', '100000.66/ziKCror0LSOo7o1mXKg==.HEwwkXvTGHxdP4o/k9ZzQ/VaicmZpbZOJkEQtEM843k=', 'SuperAdmin', NULL, 'System', 'Admin');
END

-- 2. Update/Ensure 'Admin' Admin (for Company 1)
IF EXISTS (SELECT * FROM Users WHERE UserName = 'Admin')
BEGIN
    UPDATE Users 
    SET PasswordHash = '100000.yALLB5xatIDygEFOed5aSg==.kraU25hMoPJKB/5oR3XXODag3I+VXvrDwwcAOSx1Qow=',
        Role = 'Admin',
        CompanyId = 1
    WHERE UserName = 'Admin';
END
ELSE
BEGIN
    INSERT INTO Users (UserName, PasswordHash, Role, CompanyId, FirstName, LastName)
    VALUES ('Admin', '100000.yALLB5xatIDygEFOed5aSg==.kraU25hMoPJKB/5oR3XXODag3I+VXvrDwwcAOSx1Qow=', 'Admin', 1, 'Company', 'Admin');
END

-- 3. Update/Ensure 'David' Client (for Company 2)
IF EXISTS (SELECT * FROM Users WHERE UserName = 'David')
BEGIN
    UPDATE Users 
    SET PasswordHash = '100000.yALLB5xatIDygEFOed5aSg==.kraU25hMoPJKB/5oR3XXODag3I+VXvrDwwcAOSx1Qow=',
        Role = 'Client',
        CompanyId = 2
    WHERE UserName = 'David';
END
ELSE
BEGIN
    INSERT INTO Users (UserName, PasswordHash, Role, CompanyId, FirstName, LastName)
    VALUES ('David', '100000.yALLB5xatIDygEFOed5aSg==.kraU25hMoPJKB/5oR3XXODag3I+VXvrDwwcAOSx1Qow=', 'Client', 2, 'David', 'User');
END

PRINT 'User credentials updated successfully.';
GO
