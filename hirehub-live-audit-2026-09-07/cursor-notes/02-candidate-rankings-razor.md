# CandidateRankings Razor Parser Error (`@foreach` ~L164)

**Workspace:** `C:\Users\allan\Documents\Examples\Recruitment`  
**Do NOT open GitHub PRs.** Minimal diff — view syntax only for this bug.  
**Source pin:** `hirehub-live-admin-matrix/CANDIDATE_RANKINGS_RAZOR_L164_NOTES.md`

---

## PROBLEM (live)

Admin URL: `https://nanosoft.africa/HireHub/F4359634/Admin/CandidateRankings`  
→ ASP.NET **Parser Error** (not 404): `Unexpected "foreach" keyword after "@" character`  
Reported at `CandidateRankings.cshtml` **line 164**.  
Shot: `hirehub-live-admin-matrix/candidate-rankings-admin-parser-error.png`  
Matrix: `ADMIN_MATRIX_LIVE.md` §3.

Page never compiles/renders. Auth/routing are fine for this URL — the `.cshtml` is invalid Razor.

**Do not conflate with:**
- `/CandidateRankings` (no Admin) → `ERR_TOO_MANY_REDIRECTS` (#22 orphan + #18 404 loop) — see note **03**.
- Insights nav missing Rankings link (Reports + Security Logs only) — #22 nav; Applications “Top Candidates” in-page sections still **PASS**.

---

## SOURCE (local / GitHub main dig)

**File:** `HR.Web/Views/Admin/CandidateRankings.cshtml`  
**Model:** `CandidateRankingsViewModel`  
**Action:** `AdminController.CandidateRankings` → `HandleCandidateRankings` (`AdminController.cs` ~65–67, `WorkflowHelpers.cs` ~16+)  
**Canonical URL:** `/{tenant}/Admin/CandidateRankings`

### Live FAIL — nested `@foreach` inside `@if { }` (L162–170)

```cshtml
@if (Model?.Positions != null)
{
    @foreach (var pos in Model.Positions)   <!-- L164 FAIL: @ inside code block -->
    {
        <option value="@pos.Id" ...>
```

### Same-pattern siblings (parser stops at first error; fix while here)

| Line (box HEAD) | Pattern |
|-----------------|--------|
| **L164** | `@foreach` inside `@if (Model?.Positions != null) { }` — **live FAIL** |
| **L179** | `@foreach` inside `@if (Model?.CandidatesByPosition != null …) { }` |
| **L202** | `@for` after `var rankedCandidates = …;` still inside `@if { }` |

**Why Razor fails:** `@if { }` opens C# code mode. Inside that block, control flow must be plain `foreach` / `for` — **no** leading `@`. `@foreach` asks for a new markup→code transition → “Unexpected foreach after @”.

**Rule of thumb:**
- Markup → code: need `@` (`@if`, `@foreach`, `@pos.Title`).
- Code → more code: **no** `@` (`foreach`, `for`, `var x = …`).
- After an HTML tag inside a block, back in markup → `@` required again (e.g. L189 `@if` inside a `<div>` is OK — leave alone).

Contrast: `EnhancedCandidateRankings.cshtml` top-level `@foreach` in markup is valid.

Box clone HEAD `b6765aa` still has these nested `@`s. **Verify local line numbers** — Allan’s tree may differ.

---

## How to reproduce

1. Log in as Admin / SuperAdmin on tenant `F4359634` (or local).
2. Open `…/Admin/CandidateRankings` (tenant-prefixed OK).
3. Expect (broken): yellow Parser Error naming `foreach` / line ~164.
4. Optional contrast: `…/CandidateRankings` (no Admin) → redirect loop (note 03), not this parser error.
5. Insights menu: confirm Rankings link absent (Reports + Security Logs only).

---

## Intended fix (safer approach; constraints)

**Do not change controllers, routes, or models for the parser error.** Only drop illegal nested `@` on control-flow keywords inside open code blocks.

### Change 1 — L164 (live FAIL)

**From:** `@foreach (var pos in Model.Positions)`  
**To:** `foreach (var pos in Model.Positions)`  
(still inside the existing `@if (Model?.Positions != null) { }`)

### Change 2 — L179

**From:** `@foreach (var positionGroup in Model.CandidatesByPosition)`  
**To:** `foreach (var positionGroup in Model.CandidatesByPosition)`

### Change 3 — L202

**From:** `@for (int i = 0; i < rankedCandidates.Count; i++)`  
**To:** `for (int i = 0; i < rankedCandidates.Count; i++)`  
(after the `var rankedCandidates = …;` statement)

### Leave alone

- `@pos.Id`, `@pos.Title`, `@(ViewBag.SelectedPositionId == …)` — expressions in markup.
- `@if` that appears **after HTML** (e.g. L189, L199) — those `@` are required.
- Filter form, AntiForgeryToken, Layout — unrelated.

### Optional same change-set (#22 nav — not required to clear parser error)

- `NavMenuBuilder.BuildInsightsMenuItems`: add Candidate Rankings → `Admin` / `CandidateRankings`.
- Optional alias redirect `{tenant}/CandidateRankings` → `Admin/CandidateRankings` (see note 03 for loop fix).

**Do not open a PR.**

---

## Files to inspect

- `HR.Web/Views/Admin/CandidateRankings.cshtml` (**primary** — L164 / L179 / L202)
- `HR.Web/Views/Admin/EnhancedCandidateRankings.cshtml` (valid contrast)
- `HR.Web/Controllers/AdminController.cs` + `AdminController.WorkflowHelpers.cs` (confirm action; usually no edit)
- `HR.Web/Services/NavMenuBuilder.cs` (Insights orphan — optional)
- Dig: `/workspace/hirehub-live-admin-matrix/CANDIDATE_RANKINGS_RAZOR_L164_NOTES.md`

---

## Success criteria

- `/Admin/CandidateRankings` as Admin renders filter + position groups (or empty state) — **no** yellow Parser Error.
- Position filter GET still posts to `Admin/CandidateRankings`.
- Nested `@foreach`/`@for` inside code blocks removed; markup-context `@` expressions unchanged.
- (If nav touched) Insights exposes Rankings → Admin action.

---

## Cursor: verify and reason

In local `C:\Users\allan\Documents\Examples\Recruitment`, open `CandidateRankings.cshtml` and confirm the three nested-`@` sites (line numbers may have shifted). Hypothesis: view-only Razor transition bug; controllers/routes are innocent for the Parser Error. Fix by dropping `@` before `foreach`/`for` inside `@if { }` / after C# statements — do **not** rewrite the page or invent a new controller. Separately: bare `/CandidateRankings` + redirect loop = notes **03** / #18/#22, not this fix. **Do not open a PR.**
