# HireHub (NanoHireHub) — Cursor paste-prompt notes

**Date:** 2026-09-07 (Africa/Nairobi)  
**Work in:** `C:\Users\allan\Documents\Examples\Recruitment` (Allan’s local checkout — may differ from box clone HEAD `b6765aa` / June 2026)  
**Live tenant:** `https://nanosoft.africa/HireHub/F4359634/`  
**Rules:** PROBLEM + FIX (not fix-only). Minimal diff. **Do NOT open GitHub PRs.** Do **not** use `[ValidateInput(false)]` for emails. Keep `EmailBodyHtmlSanitizer`. Invite Cursor to verify/reason against local source.

**Dig sources incorporated:**  
`hirehub-live-admin-matrix/*` (incl. `CANDIDATE_RANKINGS_RAZOR_L164_NOTES.md`), `hirehub-live-audit-2026-09-07/{SUMMARY,SOURCE_CHECKLIST}.md`, `hirehub-live-coverletter-email/ALLOWHTML_BODY_FIX_NOTES.md`, `hirehub-applicant-ronald/APPLICANT_LOG.md`, `/workspace/Recruitment-module` spot-checks.

---

## Notes index

| # | File | 1-line summary |
|---|------|----------------|
| 01 | [01-allowhtml-email-sends.md](01-allowhtml-email-sends.md) | Live Template Send → `HttpRequestValidationException` on HTML `body`; bind `[AllowHtml]` VM + keep sanitizer |
| 02 | [02-candidate-rankings-razor.md](02-candidate-rankings-razor.md) | `/Admin/CandidateRankings` Razor Parser Error `@foreach` ~L164 inside `@if { }` |
| 03 | [03-redirect-loop-404.md](03-redirect-loop-404.md) | Bad URLs → `ERR_TOO_MANY_REDIRECTS` via `/Error/NotFound` without `TrySkipIisCustomErrors` + TenantFilter ping-pong |
| 04 | [04-applications-applicant-ctas.md](04-applications-applicant-ctas.md) | Applicant Applications cards display-only (no View / Continue / Open Questionnaire) |
| 05 | [05-departments-edit-permissions.md](05-departments-edit-permissions.md) | Applicant sees Departments Edit → Login redirect; `canManage` wrongly true |
| 06 | [06-emailtemplates-tinymce.md](06-emailtemplates-tinymce.md) | EmailTemplates TinyMCE ×N eager init → Chrome Aw Snap / tab kill |
| 07 | [07-reports-preview-generate.md](07-reports-preview-generate.md) | Reports Generate writes HTML as `.pdf`; #13 modal under `.app-page` / no AJAX timeout |
| 08 | [08-profiledetails-experience-ux.md](08-profiledetails-experience-ux.md) | Experience `step=0.5` rejects `12.00`/`8.00`; mid-flow app absent until Finish (by design) |

---

## Suggested fix order (ship risk / user trust)

1. **03 Redirect loop / branded 404** — any bad URL breaks the browser; blocks Rankings orphan too  
2. **02 CandidateRankings Razor** — Admin rankings page unusable (parser error)  
3. **04 Applications applicant CTAs** — dead cards for every applicant  
4. **06 EmailTemplates TinyMCE lazy-init** — tab-killing on admin open  
5. **01 AllowHtml email Sends** — admin cannot send rich interview/failed mail  
6. **05 Departments Edit permissions** — auth/UI leak → Login challenge  
7. **07 Reports Preview/Generate** — HTML-as-PDF + modal hardening (#13)  
8. **08 ProfileDetails experience UX** (+ optional deferred-Application product note)

Nav orphan for Rankings (Insights missing CandidateRankings link) can ship with **02** or **03** — see both notes.

---

## How to use

Paste one note at a time into Cursor Agent on Allan’s PC with the workspace opened at `C:\Users\allan\Documents\Examples\Recruitment`.  
Ask Cursor to **confirm root cause in the local checkout** before editing (local may already contain partial fixes not on GitHub `main` @ `b6765aa`).  
After each fix: build/run locally if possible; do **not** open a PR unless Allan explicitly asks.
