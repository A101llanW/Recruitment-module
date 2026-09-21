# ProfileDetails experience step 0.5 + deferred Application UX (shorter)

**Workspace:** `C:\Users\allan\Documents\Examples\Recruitment`  
**Do NOT open GitHub PRs.** Optional / lower priority than notes 01–07. Minimal diff.

---

## PROBLEM (live)

Applicant Shazah apply flow on position **3022** (`QA CoverLetter Probe 20260907`), 2026-09-07:

1. **ProfileDetails experience FAIL:** fields showed `12.00` / `8.00` (existing profile). Browser validation: **“Please enter a multiple of 0.5”** on both Total / Relevant years. Re-entering `12` and `8` passed.  
   Shots: `FAIL-profiledetails-experience-step.png`, `profiledetails-3022-before-save.png`.

2. **Applications Index missing 3022 while on Questionnaire:** after Continue past ProfileDetails, Questionnaire opened (empty questions — expected if no stage-1 `PositionQuestions`), but Applications list still showed only prior apps (e.g. Senior DevOPS). Looks FAIL; **by design** — Application row is INSERTed only on **FinishQuestionnaire**, not on Cover Letter / ProfileDetails Continue.

Digs: `APP_MISSING_AFTER_COVERLETTER_SOURCE.md`; `APPLICANT_LOG.md` (Shazah sections).

---

## SOURCE (local / GitHub main dig)

**Experience 0.5**

- `Views/Applications/ProfileDetails.cshtml` ~203–216:  
  `TotalYearsExperience` / `RelevantYearsExperience` — `type="number"`, **`step="0.5"`**, `min="0"`.
- ViewModel: `[Range(0, 60)]` (`ApplicantProfileViewModel.cs` ~27–33) — no step attribute conflict in C#, but HTML5 step rejects values that browsers treat as non-multiples when displayed with trailing `.00` (or float formatting quirks).
- ProfileDetails is mandatory mid-step: CoverLetter → ProfileDetails → Questionnaire; also re-entry gate if profile incomplete (`Helpers.cs` RequireCompleteApplicantProfile).

**Deferred Application write**

| Stage | Persists Application? |
|-------|------------------------|
| Cover Letter Continue | No — Session |
| ProfileDetails Continue | No — Applicant / ApplicantProfile only |
| Questionnaire GET/POST | No — Session answers |
| **FinishQuestionnaire** | **Yes** — `CreateApplicationFromQuestionnaire` / `Applications.Add` |

Applicant Index (`GetApplicantApplications`) has **no** status/stage filter hiding mid-flow apps — row simply does not exist yet. Empty Questionnaire UI is expected when no stage-1 questions (`Questionnaire.cshtml` warning).

---

## How to reproduce

1. Applicant with saved experience displayed as `N.00` on ProfileDetails for a position apply.
2. Click Save Profile & Continue without retyping → HTML5 “multiple of 0.5” on step=0.5 fields.
3. Continue to Questionnaire; open Applications Index → new position absent until Finish Application succeeds.
4. If position has no stage-1 questions → empty Questionnaire warning (data issue, not Index filter).

---

## Intended fix (safer approach; constraints)

**A. Experience UX (preferred small fix)**

- Hypothesis: browser step validation + formatted `12.00` display fails `step="0.5"` in some browsers.
- Options (pick one after verifying local behavior):  
  - Format/display values without unnecessary `.00` (e.g. `12` / `12.5`);  
  - Use `step="any"` or `step="0.1"` if half-years still desired via server validation;  
  - Normalize on GET so bound value is an exact multiple of 0.5 in the DOM;  
  - Keep `step="0.5"` but ensure `value` attribute is `12` not `12.00`.
- Keep `[Range(0, 60)]` server-side. Do not remove experience completeness gates without product OK.

**B. Missing app mid-flow (product — optional, larger)**

- **UX-only:** document / banner “Application in progress” from Session (`CoverLetterPositionId`) without DB; improve applicant card CTAs (note **04**).
- **Persist earlier:** draft Application on CoverLetter or ProfileDetails Continue (`Draft`/`InProgress`), update on Finish — touches create/workflow/`HasExistingApplication` (see dig §Concrete fix B). Do **not** chase Index filters — none hide the row.

**C.** Assign stage-1 questions on probe positions when testing questionnaire UX (data, not code).

**Do not open a PR.**

---

## Files to inspect

- `HR.Web/Views/Applications/ProfileDetails.cshtml` (~207, ~214)
- `HR.Web/ViewModels/ApplicantProfileViewModel.cs`
- `HR.Web/Controllers/ApplicationsController.cs` (ProfileDetails POST ~301–360; FinishQuestionnaire ~195–259)
- `HR.Web/Controllers/ApplicationsController.Helpers.cs` (CreateApplicationFromQuestionnaire ~403–428; GetApplicantApplications ~544–559)
- `HR.Web/Views/Applications/Questionnaire.cshtml` (empty-state ~114–116)
- Dig: `APP_MISSING_AFTER_COVERLETTER_SOURCE.md`

---

## Success criteria

- Existing whole-number (and valid .5) experience values submit without “multiple of 0.5” false rejects.
- Mid-flow “missing application” either explained in UI or draft row exists — Index filter changes alone will not help.
- No regression on ProfileDetails → Questionnaire happy path.

---

## Cursor: verify and reason

Reproduce the `12.00` + `step="0.5"` interaction in the local browser before changing validation. Confirm Application INSERT still only on FinishQuestionnaire. Prefer a tiny display/step fix for experience; treat early draft Application as a product decision. Local checkout may already differ from `b6765aa`. **Do not open a PR.**
