# Local development tools

These scripts live under `tools/dev/` and are **not** part of the IIS / publish output.

## Local configuration (not in Git)

`HR.Web\Web.config` and `HR.Web\secrets.config` are **gitignored**. Copy the examples after clone:

```powershell
copy HR.Web\Web.config.example HR.Web\Web.config
copy HR.Web\secrets.config.example HR.Web\secrets.config
```

Edit `Web.config` for your SQL connection string and `secrets.config` for SMTP / API keys. Never commit real credentials.

## Verify SuperAdmin login

Confirms a global user's password hash matches input (same logic as the web app). Does not call `/Account/Login` (CAPTCHA/MFA still apply in the browser).

```powershell
# From repository root — password is required (no default in the script)
.\tools\dev\Verify-SuperAdminLogin.ps1 -Password 'YourPasswordHere'
```

Build `HR.Web` first so `HR.Web\bin\HR.Web.dll` exists.

## Local development build (Debug)

F5 / IIS Express must use a **Debug** build in `HR.Web\bin\`. Release output goes to `HR.Web\bin\Release\` so publish builds do not overwrite your debug DLL.

```powershell
.\tools\dev\Build-Dev.ps1
```

In Visual Studio, set the solution configuration to **Debug** (not Release) before debugging. `Web.config` should have `AppEnvironment` = `Remote/Dev` and `compilation debug="true"`.

## Publish package (IIS)

Sync a full Release deploy tree to `Publish\` (bin, Views, Content, Scripts, configs; excludes dev SQL/PS1 and debug views):

```powershell
.\tools\dev\Sync-Publish.ps1
```

Use `-SkipBuild` to mirror only, or `-UpdateSecrets` to copy `HR.Web\secrets.config` into `Publish\`.

`Sync-Publish.ps1` applies `Web.Release.config` settings to `Publish\Web.config` (`AppEnvironment=Production`, `compilation debug="false"`, `customErrors mode="On"`). Your local `HR.Web\Web.config` stays on `Remote/Dev` for F5 debugging.

## Database schema (SQL-only)

Schema is applied **only** via SQL scripts — the app does not run EF `DbMigrator` or runtime schema patching at startup.

After pulling schema changes:

```powershell
# Regenerate complete bootstrap script when model/SQL inputs change
.\tools\dev\Build-CompleteDatabaseScript.ps1

# Apply incremental SQL against local DB (reads connection from HR.Web\Web.config)
.\HR.Web\Scripts\Apply-MigrationsSql.ps1 -ServerInstance ".\SQLEXPRESS" -Database "HR_Local" -WindowsAuth

# Verify — expect zero missing columns
sqlcmd -S .\SQLEXPRESS -E -d HR_Local -b -i HR.Web\Migrations\Verify-ModelColumns.sql
```

Deprecated (do not use): `Apply-EfMigrations.ps1`, `Update-EfDatabase.ps1`, `Run-EfMigrate.ps1`, `Update-Database` in Package Manager Console.

If Company Details (or other pages) fail with *"The model backing the 'HrContext' context has changed since the database was created"*, run the SQL workflow above.

Smoke-test the Company Details EF queries:

```powershell
.\tools\dev\Test-CompanyDetailsLoad.ps1 -CompanyId 3012
```

## Email / SMTP

Use `Infrastructure/Test-SMTP.ps1` or `TestEmailConfig.ps1` at the repository root (read credentials from `secrets.config` only — never commit secrets).

## Removed from `HR.Web` for production

- `Scripts/Test-SuperAdminLogin.ps1` (moved here; no default password)
- `Utilities/SmtpTest.cs` (contained hardcoded SMTP credentials)
- Runtime logs: `mfa_codes.txt`, `verification_codes.txt`, `email_errors.txt`
- Debug views: `Views/Home/Debug.cshtml`, `Views/Home/Index.cshtml` (Debug builds only)
