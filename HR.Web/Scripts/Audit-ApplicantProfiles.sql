/*
  ApplicantProfiles audit — run against your HR database (e.g. HR_Local).

  Sections:
  1) Summary counts
  2) Applicants with no ApplicantProfiles row
  3) Applicants with applications but no profile (likely incomplete Profile Details step)
  4) Duplicate applicant emails within the same company (normalized)
  5) Detail: all applicant rows in duplicate email groups + profile presence
*/

SET NOCOUNT ON;

PRINT N'=== 1) Summary ===';
SELECT
    (SELECT COUNT(*) FROM dbo.Applicants) AS ApplicantRowCount,
    (SELECT COUNT(*) FROM dbo.ApplicantProfiles) AS ApplicantProfileRowCount,
    (SELECT COUNT(*) FROM dbo.Applicants a LEFT JOIN dbo.ApplicantProfiles p ON p.ApplicantId = a.Id WHERE p.Id IS NULL) AS ApplicantsMissingProfile;

PRINT N'=== 2) Applicants with no ApplicantProfiles row ===';
SELECT a.Id AS ApplicantId,
       a.CompanyId,
       a.Email,
       a.FullName
FROM dbo.Applicants a
LEFT JOIN dbo.ApplicantProfiles p ON p.ApplicantId = a.Id
WHERE p.Id IS NULL
ORDER BY a.CompanyId, a.Email;

PRINT N'=== 3) Applicants with at least one Application but no ApplicantProfiles row ===';
SELECT a.Id AS ApplicantId,
       a.CompanyId,
       a.Email,
       a.FullName,
       COUNT(app.Id) AS ApplicationCount
FROM dbo.Applicants a
INNER JOIN dbo.Applications app ON app.ApplicantId = a.Id
LEFT JOIN dbo.ApplicantProfiles p ON p.ApplicantId = a.Id
WHERE p.Id IS NULL
GROUP BY a.Id, a.CompanyId, a.Email, a.FullName
ORDER BY ApplicationCount DESC, a.CompanyId, a.Email;

PRINT N'=== 4) Duplicate emails within same company (normalized lower + trim) ===';
SELECT a.CompanyId,
       LOWER(LTRIM(RTRIM(a.Email))) AS EmailNormalized,
       COUNT(*) AS ApplicantRowCount
FROM dbo.Applicants a
WHERE a.Email IS NOT NULL AND LTRIM(RTRIM(a.Email)) <> ''
GROUP BY a.CompanyId, LOWER(LTRIM(RTRIM(a.Email)))
HAVING COUNT(*) > 1
ORDER BY ApplicantRowCount DESC, a.CompanyId;

PRINT N'=== 5) Duplicate-email groups: each applicant + profile status ===';
;WITH Dup AS (
    SELECT a.CompanyId,
           LOWER(LTRIM(RTRIM(a.Email))) AS EmailNormalized
    FROM dbo.Applicants a
    WHERE a.Email IS NOT NULL AND LTRIM(RTRIM(a.Email)) <> ''
    GROUP BY a.CompanyId, LOWER(LTRIM(RTRIM(a.Email)))
    HAVING COUNT(*) > 1
)
SELECT a.Id AS ApplicantId,
       a.CompanyId,
       a.Email,
       a.FullName,
       CASE WHEN p.Id IS NULL THEN 0 ELSE 1 END AS HasApplicantProfile,
       p.UpdatedOn AS ProfileUpdatedOnUtc
FROM dbo.Applicants a
INNER JOIN Dup d ON d.CompanyId = a.CompanyId
    AND LOWER(LTRIM(RTRIM(a.Email))) = d.EmailNormalized
LEFT JOIN dbo.ApplicantProfiles p ON p.ApplicantId = a.Id
ORDER BY a.CompanyId, d.EmailNormalized, a.Id;

GO
