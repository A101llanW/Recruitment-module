# Departments Edit shown to applicants → Login redirect

**Workspace:** `C:\Users\allan\Documents\Examples\Recruitment`  
**Do NOT open GitHub PRs.** Minimal diff preferred. Do **not** weaken server `[Authorize]` on Edit/Update.

---

## PROBLEM (live)

Applicant on `…/Departments/Details/4` (2026-09-07):

1. **Edit** link visible (`/Departments/Edit/4`).
2. Click Edit → redirect to  
   `…/Account/Login?ReturnUrl=%2FHireHub%2FF4359634%2FDepartments%2FEdit%2F4`  
   — **not** 403 / Access Denied, not the edit page.
3. Expected product behavior: hide admin Edit from applicant Departments views.

Evidence: `hirehub-applicant-ronald/APPLICANT_LOG.md` §6 / §6b; shot `FAIL-department-edit-redirect-login.png`.  
Related: Bug #6 / local prompt 6.

Index also risks showing New/Edit/Delete because `canManageDepartments` is wrongly **true** for applicants.

---

## SOURCE (local / GitHub main dig)

Full dig: `hirehub-live-admin-matrix/DEPARTMENTS_EDIT_APPLICANT_SOURCE.md`.

| Layer | Applicant behavior |
|-------|-------------------|
| **`RolePermissionService.CanCurrentUserAccessModule`** (~41–49) | For any non-Admin base role (including Applicant): special-case Client+Reports → false; **else `return true`** → Manage/View Departments **allowed** |
| **Index** | New/Edit/Delete gated on `canManageDepartments \|\| IsInRole("HR")` — flag true → links show |
| **Details** | **Edit always rendered** (no canManage check). `Details()` does **not** set `ViewBag.CanManageDepartments`. QA path used this. |
| **Edit/Update** | `[Authorize(Roles = "Admin, SuperAdmin")]` runs first → authenticated wrong role → **401 → Forms Login redirect**, not 403. `[RoleBasedAuthorization]` / `[ModuleAccess]` never emit 403 after Authorize fails. |

Contrast: `Views/Positions/Details.cshtml` gates Edit with `@if (canManagePositions)`.  
Positions Index also ANDs `User.IsInRole("Admin")` with module Manage — Departments Index does **not**.

Server POSTs are not open; FAIL is **UI leak + Login challenge**.

---

## How to reproduce

1. Log in as applicant.
2. Nav → Departments → Details on any department.
3. Observe Edit button/link. Click it.
4. Expect (broken): Login page with ReturnUrl to Departments/Edit/{id}.
5. Optional: Departments Index — New/Edit/Delete may also show.

---

## Intended fix (safer approach; constraints)

1. **`Views/Departments/Details.cshtml`:** wrap Edit (and any Delete) in `@if (canManageDepartments)` — mirror Positions Details.
2. **`DepartmentsController.Details`:** set `ViewBag.CanManageDepartments` / `CanViewDepartments` the same way Index does — **after** Manage is computed correctly.
3. **`Views/Departments/Index.cshtml`:** gate New/Edit/Delete on **`canManageDepartments` only**; drop `\|\| User.IsInRole("HR")` (HR is not in Edit’s Authorize roles anyway).
4. **Make `CanManageDepartments` false for applicants** (otherwise Index “hide when !canManage” still shows Edit):
   - Prefer aligning with Positions Index: require Admin (or Manage-capable admin role) **before** trusting `CanCurrentUserAccessModule`, **or**
   - Fix `RolePermissionService` so non-Admin (Applicant/Client) does **not** get blanket module Manage/View (except intentional product modules). Broader change — reason carefully; may affect other modules/nav.
5. **Leave** Edit/Update `[Authorize]` + `[RoleBasedAuthorization]` in place (do not open POSTs).
6. Optional follow-up: authenticated denial → 403 via RoleBasedAuthorization/ModuleAccess instead of Authorize→Login (explains QA “expect 403”; not required once Edit is hidden).

**Do not open a PR.**

---

## Files to inspect

- `HR.Web/Views/Departments/Details.cshtml` (~61–63 ungated Edit)
- `HR.Web/Views/Departments/Index.cshtml` (New/Edit/Delete gates)
- `HR.Web/Controllers/DepartmentsController.cs` (Index flags ~22–25; Details ~29–44; Edit auth ~85+)
- `HR.Web/Services/RolePermissionService.cs` (~41–49 non-Admin → true)
- `HR.Web/Views/Positions/Details.cshtml` (reference pattern)
- `HR.Web/Filters/RoleBasedAuthorizationAttribute.cs`, `ModuleAccessAttribute.cs`
- `HR.Web/Services/NavMenuBuilder.cs` (why applicants see Departments at all)

---

## Success criteria

- Applicant Details/Index: **no** Edit/New/Delete when not Manage-capable.
- Applicant deep-link to Edit still denied server-side (Login or 403 — prefer hide so they never hit it).
- Admin/SuperAdmin still see and use Edit.
- `canManageDepartments` false for Applicant after permission/flag fix.

---

## Cursor: verify and reason

Confirm local Details still renders Edit unconditionally and whether `RolePermissionService` still blanket-allows non-Admin Manage. Hypothesis: false `canManage` + ungated Details Edit + Authorize→Login explains live. Prefer UI gate + correct Manage flag; do not strip Authorize. If fixing RolePermissionService, enumerate other modules that rely on non-Admin `return true` before changing globally. **Do not open a PR.**
