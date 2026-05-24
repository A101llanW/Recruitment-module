# Codacy Setup for This Repository

This repository now includes:

- GitHub workflow: `.github/workflows/codacy-analysis.yml`
- Windows launcher for Codacy CLI v2: `.codacy/cli.ps1`
- Cursor MCP example config: `Docs/cursor-mcp.codacy.example.json`

## Cursor MCP (Codacy)

1. Create a Codacy **Account API token** in Codacy → Account → API Tokens.
2. Set it as a Windows user environment variable:

```powershell
[System.Environment]::SetEnvironmentVariable('CODACY_ACCOUNT_TOKEN', '<your-token>', 'User')
```

3. Ensure MCP config exists at `.cursor/mcp.json` (project) or `%USERPROFILE%\.cursor\mcp.json` (global). On Windows, Cursor currently expects the token value inline in `env.CODACY_ACCOUNT_TOKEN` (not `${env:...}` substitution).

Quick setup script:

```powershell
.\tools\dev\Set-CodacyMcpToken.ps1 -Token '<your-account-api-token>'
```

4. Restart Cursor.
5. Verify in **Cursor Settings → MCP** that `codacy` is connected and tools are listed.

### MCP troubleshooting

| Symptom | Fix |
|---------|-----|
| `Unauthorized` on API tools | Regenerate Account API token and update `CODACY_ACCOUNT_TOKEN` |
| MCP tools not visible to agent | Restart Cursor; check Settings → MCP connection status |
| Local analyze works, API fails | Token issue — local CLI does not need API for basic scans |
| Agent says MCP unavailable | Server identifier is `user-codacy`; config name is `codacy` |

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
