# Redirect loop on 404 / bad Admin / CandidateRankings URLs

**Workspace:** `C:\Users\allan\Documents\Examples\Recruitment`  
**Do NOT open GitHub PRs.** Minimal diff preferred.

---

## PROBLEM (live)

Applicant (and any session) deep-dive 2026-09-07 on `https://nanosoft.africa/HireHub/F4359634/`:

| URL | Live result |
|-----|-------------|
| `/ThisDoesNotExistXYZ` | `ERR_TOO_MANY_REDIRECTS` (Chrome “redirected you too many times”) — **not** branded 404 |
| `/Admin/ThisDoesNotExistXYZ` (missing action) | same redirect storm |
| `/zzz/nope` | same |
| `/CandidateRankings` (orphan, no controller) | same (`candidate-rankings-redirect-loop.png`) |
| `/Admin/CandidateRankings` (action **exists**) | auth → Login for applicant — **PASS** (no loop) |

Shots: `hirehub-applicant-ronald/FAIL-404-*.png`, `hirehub-live-admin-matrix/candidate-rankings-redirect-loop.png`.

Not a clean branded “Page Not Found”. Related bugs **#18** (branded 404) and **#22** (Rankings orphan URL).

---

## SOURCE (local / GitHub main dig)

Full dig: `hirehub-live-admin-matrix/REDIRECT_LOOP_404_SOURCE.md`.

**Stacked root cause:**

1. `ErrorController.NotFound` (and `HomeController.NotFound` twin) set `Response.StatusCode = 404` but **never** `Response.TrySkipIisCustomErrors = true`. Only `AntiForgeryExceptionFilterAttribute` sets the skip flag today.
2. Production `customErrors` / IIS `httpErrors` **redirect** on 404. Live terminates on `/Error/NotFound`. That action re-emits 404 → another 302 → infinite loop.
3. **Amplifier:** `TenantFilterAttribute` 302s authenticated company users from tenant-less `/Error/NotFound` to `/{slug}/Error/NotFound`; customErrors then strips tenant back → ping-pong.

Repo transforms (`Web.Release.config` / `Sync-Publish.ps1`) still point 404 → `~/Account/Login` (wrong UX); live IIS appears to target `/Error/NotFound` instead. `Web.config` is **gitignored** — inspect local/live IIS separately.

Missing controllers/actions are **triggers**, not the loop itself. Canonical rankings URL is `Admin/CandidateRankings`; bare `/CandidateRankings` 404s into the loop (#22).

---

## How to reproduce

1. As logged-in applicant (or admin): open  
   `…/HireHub/F4359634/ThisDoesNotExistXYZ`  
   Expect (broken): Chrome redirect loop, not branded NotFound.
2. Same for `…/Admin/NoSuchAction`, `…/CandidateRankings`.
3. Contrast: `…/Admin/CandidateRankings` as applicant → Login challenge (no loop). As admin → rankings page (or Razor parser error — separate note 02).

Local Debug (`customErrors Off`) often **won’t** reproduce the loop — verify with Release-like `customErrors On` + `httpErrors` Custom/Auto, or reason from source.

---

## Intended fix (safer approach; constraints)

**A. Stop the loop (#18) — required**

1. On `ErrorController` (`Index` / `NotFound` / `Forbidden`) **and** `HomeController` error twins: set  
   `Response.TrySkipIisCustomErrors = true;` **before** setting `StatusCode`.  
   Pattern: `AntiForgeryExceptionFilterAttribute.cs` (~39). Keep branded status codes.
2. Align Production config (`Web.Release.config`, `Sync-Publish.ps1`, **and** local/live `Web.config` if present):  
   - 404 → branded `~/Error/NotFound` (or `~/Home/NotFound`), **not** `~/Account/Login`.  
   - Prefer `redirectMode="ResponseRewrite"` **and/or** `httpErrors existingResponse="PassThrough"` so IIS does not 302-replace the MVC response.  
   - Avoid IIS child entries that Redirect to `/Error/NotFound`.
3. `TenantFilterAttribute`: skip tenant re-injection for Error (and Home Error/NotFound/Forbidden) so tenant-less error URLs cannot ping-pong.
4. Optional: RouteConfig catch-all `{*url}` → `Error/NotFound` (still need skip flag).
5. Optional: `IsPublicAuthPath` include `/error/` (session edge cases).

**B. Rankings orphan (#22) — can ship with note 02**

- Insights nav: add CandidateRankings → `Admin` / `CandidateRankings` in `NavMenuBuilder.BuildInsightsMenuItems` (today: Reports + Security Logs only).
- Optional alias: `{tenant}/CandidateRankings` → redirect to `Admin/CandidateRankings` (do **not** invent a second controller).

Do **not** remove branded NotFound views. Do **not** open a PR.

---

## Files to inspect

- `HR.Web/Controllers/ErrorController.cs` (NotFound ~16–20)
- `HR.Web/Controllers/HomeController.cs` (Error/NotFound/Forbidden twins)
- `HR.Web/Filters/AntiForgeryExceptionFilterAttribute.cs` (TrySkip pattern)
- `HR.Web/Helpers/TenantFilterAttribute.cs` (~97–109)
- `HR.Web/App_Start/RouteConfig.cs`
- `HR.Web/Web.Release.config`, `Web.Debug.config`, local `Web.config` if present
- `tools/dev/Sync-Publish.ps1`
- `HR.Web/Services/NavMenuBuilder.cs` (~549–569 Insights)
- `HR.Web/Controllers/AdminController.cs` (`CandidateRankings`)

---

## Success criteria

- Logged-out + logged-in: `/ThisDoesNotExistXYZ`, bad `/Admin/*`, `/CandidateRankings` → **single** branded 404 page, **no** redirect storm.
- `/Admin/CandidateRankings` as applicant → Login (unchanged).
- Admin deep link → rankings UI (after Razor fix if needed).
- Insights can reach rankings without guessing the orphan URL.

---

## Cursor: verify and reason

Confirm local ErrorController still lacks `TrySkipIisCustomErrors` and whether live/local `Web.config` 404 target is Login vs Error/NotFound (transforms may disagree with IIS). Hypothesis: loop = 404 page re-asserting 404 under redirect-mode customErrors, worsened by TenantFilter. Prefer skip-flag + PassThrough/Rewrite over inventing new redirect chains. Local may already have partial #18 work — verify before editing. **Do not open a PR.**
