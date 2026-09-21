# EmailTemplates — TinyMCE eager init ×N (Aw Snap / tab kill)

**Workspace:** `C:\Users\allan\Documents\Examples\Recruitment`  
**Do NOT open GitHub PRs.** Minimal diff preferred.

---

## PROBLEM (live)

Admin Email Templates on live HireHub (2026-09-07 audit):

- Opening EmailTemplates caused **Chrome Aw Snap (error 9)** / tab kill — session could not complete the EmailTemplates check (SUMMARY: **SKIPPED** after Aw Snap).
- Product impact: admin cannot reliably edit catalog email bodies.

Live UI may already show a “Click Edit to load…” lazy placeholder on some deploys; box GitHub main still has eager init — **treat live vs local separately**.

---

## SOURCE (local / GitHub main dig)

`SOURCE_CHECKLIST.md` §2 — **OPEN** on HEAD `b6765aa`:

- `HR.Web/Views/Admin/EmailTemplates.cshtml`
- On `DOMContentLoaded` (~705+): `while (true)` loop over `defaultBodyTemplate_0…N` (~762+)
- Each found textarea: `tinymce.init({ selector: '#' + bid, height: 340, plugins: '… autoresize …', … })`
- Catalog N ≈ **10** → ~10 heavy TinyMCE instances at once + `autoresize` + `editor.save()` on `change keyup SetContent`
- TinyMCE loaded from CDN (~390): `tinymce@6.8.3`

Hypothesis: eager multi-editor init exhausts memory/CPU → Aw Snap. Lazy-init on Edit only is the safer UX.

---

## How to reproduce

1. Log in as Admin.
2. Open Admin → Email Templates (exact nav label may vary).
3. Expect (broken): tab crash / Aw Snap, or severe jank with many editors visible at once.
4. If local already lazy-loads: note that and skip redundant work — verify live separately.

---

## Intended fix (safer approach; constraints)

1. **Lazy-init TinyMCE:** do not `tinymce.init` every `defaultBodyTemplate_N` on DOMContentLoaded.
2. On first **Edit** (or expand/compose) for a template row: init that textarea’s editor once; destroy/hide when Cancel if product allows.
3. Keep `tinymce.triggerSave()` / `editor.save()` on the form submit path for editors that **were** initialized.
4. Preserve existing toolbar/plugins/valid_elements behavior for a single editor — do not redesign the template catalog.
5. Optional: placeholder “Click Edit to load editor…” in the body area until init.
6. Minimal JS change in `EmailTemplates.cshtml` (or extracted script) — avoid unrelated Admin refactors.

**Do not open a PR.** Do not disable TinyMCE globally.

---

## Files to inspect

- `HR.Web/Views/Admin/EmailTemplates.cshtml` (DOMContentLoaded loop ~762+, TinyMCE script ~390, compose markup ~239+)
- Any partial/helper for email template compose rows
- Contrast: interview/failed-candidate email modals (single editor) — usually OK

---

## Success criteria

- Opening EmailTemplates does **not** crash the tab.
- Editors initialize on Edit (or explicit user action), not all at once on load.
- Saving a template still persists HTML body (triggerSave works for the active editor).
- Catalog of ~10 templates remains usable.

---

## Cursor: verify and reason

Confirm local still uses the `while (true)` eager `tinymce.init` loop. If Allan already added lazy placeholders, document and only finish gaps. Hypothesis: N simultaneous TinyMCE + autoresize = Aw Snap; fix is defer init, not remove rich editing. Line numbers may differ from June HEAD. **Do not open a PR.**
