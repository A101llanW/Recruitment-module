# AllowHtml on interview / failed-candidate email Sends

**Workspace:** `C:\Users\allan\Documents\Examples\Recruitment`  
**Do NOT open GitHub PRs.** Minimal diff. **Do NOT** use `[ValidateInput(false)]`. **Keep** `EmailBodyHtmlSanitizer.Sanitize`.

---

## PROBLEM (live)

On live `https://nanosoft.africa/HireHub/F4359634/` (admin session 2026-09-07):

1. **Interviews → Email Candidate → Use Template → Send**  
   POST `…/Interviews/SendInterviewCandidateEmail` → yellow ASP.NET page:  
   `System.Web.HttpRequestValidationException` — “A potentially dangerous Request.Form value was detected” for HTML body (`<p>Dear AMW AMW,</p>…`). Email never sends.  
   Shot: `hirehub-live-admin-matrix/interviews-template-send.png`

2. **Applications → failed-candidate Email → Use Template → Send**  
   POST `…/Applications/SendFailedCandidateEmail` → same `HttpRequestValidationException` on HTML `body`.  
   Shot: `hirehub-live-admin-matrix/applications-template-send.png`

3. **Compose from Scratch → Send** (both surfaces)  
   Client-side “Please enter an email body before sending.” — POST never leaves the page (`Interviews` / `Applications` URL unchanged). TinyMCE content not reaching the posted `body` field / client validation. Separate but related UX bug; Template path is the validation exception.

**Cause (live):** AllowHtml body binding is **not deployed**. TinyMCE posts HTML in form field `body`; ASP.NET request validation rejects it **before** the action runs.

---

## SOURCE (local / GitHub main dig)

Box clone `/workspace/Recruitment-module` @ `b6765aa` (June HEAD) still **OPEN**:

- Four POSTs read `Request.Form["body"]` / `["subject"]` directly with **no** `[AllowHtml]` model and **no** `[ValidateInput(false)]`:
  - `HR.Web/Controllers/InterviewsController.CandidateEmail.cs`  
    — `SendInterviewCandidateEmail` (~411+), `SendInterviewCandidatesBatchEmail` (~506+)
  - `HR.Web/Controllers/ApplicationsController.FailedCandidateEmail.cs`  
    — `SendFailedCandidateEmail` (~373+), `SendFailedCandidatesBulkEmail` (~525+)
- Both paths already call `EmailBodyHtmlSanitizer.Sanitize(body)` after read — **keep that**.
- Contrast (working pattern): `EmailTemplateManagementViewModel` has `[AllowHtml]` on HTML body fields (`HR.Web/ViewModels/EmailTemplateManagementViewModel.cs`).
- GitHub draft PR #3 is docs-only WIP (`docs/wip-allowhtml-email-send-body.md`) — **not merged**; do not assume it landed in Allan’s local tree.

Allan’s local checkout may already differ — **verify before editing**.

---

## How to reproduce

1. Log in as admin on tenant `F4359634` (or local equivalent).
2. **Interviews:** open a candidate email modal → Use Template → ensure TinyMCE shows HTML → Send once.  
   Expect (broken): yellow `HttpRequestValidationException` on `body`.
3. **Applications:** open failed-candidate email → Use Template → Send once. Same yellow page.
4. Optional: Compose from Scratch with TinyMCE filled → Send → client “enter email body” (no POST).

---

## Intended fix (safer approach; constraints)

**Safer binding — Allan’s chosen approach** (see `ALLOWHTML_BODY_FIX_NOTES.md`):

1. Add a small POST view-model (e.g. `CandidateEmailSendForm`). Put **`[AllowHtml]` ONLY on the `Body` string**. Subject stays plain (no AllowHtml). Include other posted fields these actions already read: `ApplicationId` (and batch ids), `Subject`, `Body`, `ComposeMode`, `TemplateKey`, `IncludePanelistCc`, `IncludeHrCc`, `SelectedPanelistIds`, `SelectedHrCcIds`.
2. Change the **four** actions to accept/bind that model instead of `Request.Form["body"]` / `["subject"]`.
3. Keep `[ValidateAntiForgeryToken]`, existing `[Authorize]` / `[RoleBasedAuthorization]`, and **`EmailBodyHtmlSanitizer.Sanitize` on Body after bind**.
4. Keep the same HTML form field names (`name="body"`, `name="subject"`, etc.) so model binding works.
5. Minimal diff. Prefer one shared VM used by interview + failed flows if shapes match.

**Also investigate (do not over-prescribe):** Compose-from-scratch client validation — ensure TinyMCE `triggerSave()` / hidden textarea sync before submit so `body` is non-empty when the user typed in the editor.

**Hard constraints:**
- Do **NOT** add `[ValidateInput(false)]` on these actions.
- Do **NOT** disable `requestValidation` / change global validation in `Web.config`.
- Do **NOT** remove `EmailBodyHtmlSanitizer`.
- Do **NOT** open a GitHub PR.

---

## Files to inspect

- `HR.Web/Controllers/InterviewsController.CandidateEmail.cs`
- `HR.Web/Controllers/ApplicationsController.FailedCandidateEmail.cs`
- `HR.Web/ViewModels/EmailTemplateManagementViewModel.cs` (AllowHtml pattern)
- Existing sanitizer type (search `EmailBodyHtmlSanitizer`)
- Interview / Applications email modal markup (TinyMCE `name="body"`, compose validation JS)
- Any local WIP under `docs/wip-allowhtml-email-send-body.md` if present

---

## Success criteria

- Template Send (interview + failed) POSTs HTML body, action runs, email sends (or redirects with success TempData) — **no** yellow validation exception.
- Body still passes through `EmailBodyHtmlSanitizer` before send.
- Compose-from-scratch with filled TinyMCE can POST a non-empty body (client validation no longer false-blocks).
- Auth / antiforgery unchanged; no global request-validation disable.

---

## Cursor: verify and reason

Confirm in **local** `C:\Users\allan\Documents\Examples\Recruitment` whether these four actions still use `Request.Form["body"]` or already bind an `[AllowHtml]` model (Allan may have started a fix). Prefer the Email Templates AllowHtml pattern over any broader validation bypass. Show the new view-model and the four action signatures after the change. Do not treat dig line numbers as gospel if local differs from June HEAD `b6765aa`. **Do not open a PR.**
