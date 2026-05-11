-- Panelist role workflow: seed default "Panelist" role template per company, migrate legacy Admin+IsPanelist users.
-- Idempotent for SQL Server.

SET NOCOUNT ON;

-- 1) Seed RoleDefinition "Panelist" (company-scoped) where missing
INSERT INTO dbo.RoleDefinitions (CompanyId, Name, Description, CreatedByUserName, CreatedDate, IsActive)
SELECT c.Id,
       N'Panelist',
       N'Default read-only access for interview panelists (scheduled interviews and application context).',
       N'system',
       GETDATE(),
       1
FROM dbo.Companies c
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.RoleDefinitions rd
    WHERE rd.CompanyId = c.Id
      AND rd.IsActive = 1
      AND rd.Name = N'Panelist'
);

-- 2) Permissions for company "Panelist" templates (Interviews + Applications View)
INSERT INTO dbo.RolePermissions (RoleDefinitionId, ModuleKey, AccessLevel)
SELECT rd.Id, N'Interviews', N'View'
FROM dbo.RoleDefinitions rd
WHERE rd.IsActive = 1
  AND rd.Name = N'Panelist'
  AND rd.CompanyId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleDefinitionId = rd.Id AND rp.ModuleKey = N'Interviews'
  );

INSERT INTO dbo.RolePermissions (RoleDefinitionId, ModuleKey, AccessLevel)
SELECT rd.Id, N'Applications', N'View'
FROM dbo.RoleDefinitions rd
WHERE rd.IsActive = 1
  AND rd.Name = N'Panelist'
  AND rd.CompanyId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleDefinitionId = rd.Id AND rp.ModuleKey = N'Applications'
  );

-- 3) Legacy: Admin users flagged as panelists become Role = Panelist with the company Panelist template
UPDATE u
SET u.Role = N'Panelist',
    u.IsPanelist = 0,
    u.RoleDefinitionId = map.RoleDefId
FROM dbo.Users u
INNER JOIN (
    SELECT rd.CompanyId, MIN(rd.Id) AS RoleDefId
    FROM dbo.RoleDefinitions rd
    WHERE rd.IsActive = 1
      AND rd.Name = N'Panelist'
      AND rd.CompanyId IS NOT NULL
    GROUP BY rd.CompanyId
) map ON map.CompanyId = u.CompanyId
WHERE u.IsPanelist = 1
  AND u.Role = N'Admin'
  AND u.CompanyId IS NOT NULL;

PRINT 'Panelist role workflow migration applied.';
