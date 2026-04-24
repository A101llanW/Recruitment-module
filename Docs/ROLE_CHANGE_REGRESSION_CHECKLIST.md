# Role Change Regression Checklist

## Purpose
Validate that when a user updates their own role (especially from SuperAdmin to a custom role), the session/UI updates immediately and does not keep stale SuperAdmin navigation.

## Preconditions
1. Have at least 2 companies (for cross-scope checks).
2. Have at least 1 custom role template in Company A.
3. Have these users available:
- `sa_primary`: effective SuperAdmin (global, no company).
- `sa_secondary`: second SuperAdmin (for recovery/promotion tests).
- `admin_a`: Admin in Company A.
4. Use a fresh browser session (or incognito) for each test case.

## Core Scenarios

### 1) SuperAdmin Self-Change to Custom Role (Company A)
1. Login as `sa_primary`.
2. Go to `Admin -> Global User Management`.
3. Edit `sa_primary` and set:
- Company = Company A
- Role = `custom:<company-a-role>`
4. Save.

Expected:
1. Request succeeds and redirects away from global management (to tenant admin surface).
2. Navbar no longer shows `System Administration` menu.
3. Accessing `/Companies/Index` fails authorization (no SuperAdmin anymore).
4. Company profile no longer renders SuperAdmin-only actions.
5. User remains logged in (session is refreshed, not stale).

### 2) SuperAdmin Self-Change to Built-in Admin (Company A)
1. Repeat scenario 1 but choose built-in `Admin`.

Expected:
1. Immediate redirect to tenant admin route.
2. No SuperAdmin menu items remain visible.
3. `/Companies/*` is no longer accessible.

### 3) Company Admin Self-Change to Custom Role (Same Company)
1. Login as `admin_a`.
2. Go to `Admin -> User Management`.
3. Edit `admin_a` and assign a custom role from Company A.
4. Save.

Expected:
1. Save succeeds and user stays authenticated.
2. Navbar/module visibility updates immediately to custom role permissions.
3. No stale modules from prior full-admin state remain visible.

### 4) Non-Self Edit (Control Case)
1. Login as SuperAdmin.
2. Edit another user's role (not your own).

Expected:
1. Operation succeeds.
2. Current editor session/nav does not change.
3. Only target user's next request/session reflects updated role.

## Security/Behavior Checks
1. Custom roles cannot be assigned to users without a company.
2. Company-scoped custom roles cannot be assigned across company boundaries.
3. Non-SuperAdmin cannot assign built-in `SuperAdmin`.
4. Username collisions across companies must not cause false self-update session rewrites.

## Recovery Scenario
If `sa_primary` was demoted, login as `sa_secondary` and restore `sa_primary` to `SuperAdmin`.

Expected:
1. Restored user, on next login, sees SuperAdmin UI again.
2. Global routes (`/Companies`, global user management) work again.

## Evidence to Capture
1. Before/after screenshots of navbar.
2. Redirect URL after save.
3. 403/deny response when demoted user tries `/Companies/Index`.
4. Audit log entry for role change including old/new role display.

## Quick Pass/Fail Template
- Case 1: PASS / FAIL
- Case 2: PASS / FAIL
- Case 3: PASS / FAIL
- Case 4: PASS / FAIL
- Security checks: PASS / FAIL
- Recovery: PASS / FAIL
