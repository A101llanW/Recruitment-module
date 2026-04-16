# Codacy Setup for This Repository

This repository now includes:

- GitHub workflow: `.github/workflows/codacy-analysis.yml`
- Windows launcher for Codacy CLI v2: `.codacy/cli.ps1`

## What you still need to connect

1. In Codacy, sign in with your Git provider and add this repository.
2. In GitHub, add one of these secrets:
   - `CODACY_PROJECT_TOKEN` (recommended for single-repo setup)
   - `CODACY_API_TOKEN` (useful for org-wide, multi-repo setup)
3. If you use Codacy Self-hosted, add `CODACY_API_BASE_URL` where needed in your CI environment.
4. If you plan to use client-side tools, enable "Run analysis on your build server" in Codacy repository settings.
5. Ensure your Git provider app permissions are approved for Codacy in your organization.

## Local run from this Windows environment

The existing shell launcher (`.codacy/cli.sh`) requires Bash/WSL. Use this instead from PowerShell:

```powershell
.\.codacy\cli.ps1 install
.\.codacy\cli.ps1 analyze --format sarif -o report.sarif
.\.codacy\cli.ps1 upload -s report.sarif -c <commit_sha> -t $env:CODACY_PROJECT_TOKEN
```

## Requirements checklist

- GitHub repository connected to Codacy.
- One valid Codacy token in GitHub secrets.
- CI runner outbound network access to:
  - `api.codacy.com` (or your self-hosted Codacy URL)
  - `github.com` and GitHub release/CDN endpoints used by the Codacy CLI
- Codacy permissions approved on your Git provider.
- Optional (self-hosted only): `CODACY_API_BASE_URL`.
