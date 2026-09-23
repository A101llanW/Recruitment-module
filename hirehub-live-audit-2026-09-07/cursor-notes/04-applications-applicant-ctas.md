# Applications Index — applicant card CTAs (View / Continue)

**Workspace:** `C:\Users\allan\Documents\Examples\Recruitment`  
**Do NOT open GitHub PRs.** Minimal diff preferred.

---

## PROBLEM (live)

Applicant `ronald` / Amwkjfrkjf on `…/Applications` (2026-09-07):

- Cards for applications **4038 / 4039 / 4036** (Company Secretary, QA Live MultiStage, Senior DevOPS): click = **visual selection/border only**. No View, Continue, Open Questionnaire, Profile, or navigation. URL unchanged.
- DOM: no `<a>`, no per-card `<button>`, no click handler on `#application-card-*`.
- Hover lift makes cards look clickable → UX trap.

Evidence: `hirehub-applicant-ronald/APPLICANT_LOG.md` §3; shots `FAIL-applications-no-detail-controls.png`, `FAIL-applications-card-no-detail-remaining.png`.  
Related: Positions still shows **Apply** on already-applied (#17) → server “already applied” alert — same product hole (no Continue).

**Separate (do not conflate):** mid-flow position 3022 missing from Index while on Questionnaire is **deferred Application INSERT** until FinishQuestionnaire — see note 08 / `APP_MISSING_AFTER_COVERLETTER_SOURCE.md`. Card CTA FAIL is independent.

---

## SOURCE (local / GitHub main dig)

Full dig: `hirehub-live-admin-matrix/APPLICATIONS_CARD_ACTIONS_SOURCE.md`.

- Shared view: `HR.Web/Views/Applications/Index.cshtml` (~2307 lines). No separate ApplicantIndex.
- Controller: `ApplicationsController.Index` (~363–396) → applicants get `GetApplicantApplications` (`Helpers.cs` ~544–559); `canManageApplications` false.
- **Applicant branch** `Index.cshtml` ~**1040–1104** (`else` of `@if (canManageApplications)` ~621): name, date, status chip only — **zero** action links.
- Management branch has Profile + Open next questionnaire stage; still not applicant self-service.
- Applicants **may** open own `Applications/Details/{id}` via `ValidateDetailsAccess` (`Helpers.cs` ~562–577) — but Index never links there.
- `PendingQuestionnaireStage` exists on `Application` but applicant cards never link to `Questionnaire?positionId=`.

**Hypothesis:** shared Index grew rich admin UI; applicant `else` left as read-only status strip. Source **matches** live QA.

---

## How to reproduce

1. Log in as applicant with ≥1 application.
2. Open `…/Applications`.
3. Click each application card.  
   Expect (broken): highlight only; no detail/Continue.
4. Optional: Positions → Apply on already-applied → “already applied” with no Continue (#17).

---

## Intended fix (safer approach; constraints)

1. **`Index.cshtml` applicant branch (~1040–1104):** add explicit actions per card, e.g.:
   - **View application** → `Url.Action("Details", "Applications", new { id = item.Id })` (+ tenant route values if used elsewhere).
   - **Continue / Open questionnaire** when `item.PendingQuestionnaireStage.HasValue` → `Url.Action("Questionnaire", "Applications", new { positionId = item.PositionId })`.
   - Optional: button row; keep hover only if clickable.
2. **Align #17 on `Positions/Index`:** if already applied, replace dead Apply with View / Continue using the same rules.
3. **Harden `Details.cshtml`:** hide Update Status / Delete / invite controls unless management; applicant-safe summary when they land via View.
4. Do **not** rely on admin-only “Open next questionnaire stage” / Invite for applicants.
5. Do **not** invent bulk-select UI; this is missing navigation, not deferred bulk actions.

Minimal markup + existing actions preferred. **Do not open a PR.**

---

## Files to inspect

- `HR.Web/Views/Applications/Index.cshtml` (~621 fork, ~1040–1104 applicant cards, manage Profile ~726+)
- `HR.Web/Controllers/ApplicationsController.cs` (Index, Details, Questionnaire)
- `HR.Web/Controllers/ApplicationsController.Helpers.cs` (GetApplicantApplications, ValidateDetailsAccess, ResolveExistingApplicationQuestionnaireStage)
- `HR.Web/Views/Applications/Details.cshtml`
- `HR.Web/Views/Positions/Index.cshtml` (~311 Apply → CoverLetter)
- `Models/Application.cs` (`Status`, `PendingQuestionnaireStage`, …)

---

## Success criteria

- Applicant cards expose at least **View** (own Details) and **Continue/Open questionnaire** when a pending stage exists.
- Clicking navigates (URL change / details render); not selection-only.
- Details does not expose admin Update Status/Delete to applicants.
- Positions already-applied path offers View/Continue instead of dead Apply (if touched in same change set).

---

## Cursor: verify and reason

Confirm local Index applicant branch still has no links (line numbers may shift). Prefer linking to existing `Details` / `Questionnaire` over new controllers. Check whether local already started CTAs. Invite: reason about Details.cshtml management chrome leaking to applicants who deep-link. **Do not open a PR.**
