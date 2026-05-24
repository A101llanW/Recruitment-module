# Local development tools

These scripts live under `tools/dev/` and are **not** part of the IIS / publish output.

## Verify SuperAdmin login

Confirms a global user's password hash matches input (same logic as the web app). Does not call `/Account/Login` (CAPTCHA/MFA still apply in the browser).

```powershell
# From repository root — password is required (no default in the script)
.\tools\dev\Verify-SuperAdminLogin.ps1 -Password 'YourPasswordHere'
```

Build `HR.Web` first so `HR.Web\bin\HR.Web.dll` exists.

## Email / SMTP

Use `Infrastructure/Test-SMTP.ps1` or `TestEmailConfig.ps1` at the repository root (read credentials from `secrets.config` only — never commit secrets).

## Removed from `HR.Web` for production

- `Scripts/Test-SuperAdminLogin.ps1` (moved here; no default password)
- `Utilities/SmtpTest.cs` (contained hardcoded SMTP credentials)
- Runtime logs: `mfa_codes.txt`, `verification_codes.txt`, `email_errors.txt`
- Debug views: `Views/Home/Debug.cshtml`, `Views/Home/Index.cshtml` (Debug builds only)
