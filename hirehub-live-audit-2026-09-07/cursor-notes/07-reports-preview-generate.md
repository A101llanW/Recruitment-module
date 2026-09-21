# Reports Preview / Generate (HTML-as-PDF + modal #13)

**Workspace:** `C:\Users\allan\Documents\Examples\Recruitment`  
**Do NOT open GitHub PRs.** Minimal diff preferred.

---

## PROBLEM (live)

Admin Reports on `…/ReportGenerator` / Insights → Reports (2026-09-07):

1. **Preview (Candidates):** modal opened with data this run (not stuck on “Processing Report”) — close/backdrop not fully signed off. Source still **#13 OPEN**.
2. **Generate:** Recent downloads show `candidate_*.html` content under a **`.pdf`** name / “HTML-as-PDF” risk — server Generate path writes HTML bytes to a `.pdf` file extension. Real PDF from preview uses **client** `html2pdf.js` on `#previewContent`. CSV path OK.
3. Dual controller trap: UI wires to **`ReportGenerator`**, not stub `ReportsController.GenerateDirect` (test JSON only).

Shots (matrix/audit): `preview-open.png`, `generate-result.png`, `generate-default.png`.

---

## SOURCE (local / GitHub main dig)

`SOURCE_CHECKLIST.md` §1 + SUMMARY:

| Piece | Path |
|-------|------|
| Real API | `HR.Web/Controllers/ReportGeneratorController.cs` (`Preview`, `GenerateDirect`, `Download`) |
| Stub | `HR.Web/Controllers/ReportsController.cs` — do not wire UI here |
| UI | `HR.Web/Views/Reports/Index.cshtml` |
| Service | `HR.Web/Services/ReportService.cs` |
| Nav | `NavMenuBuilder` Insights → `ReportGenerator/Index` |

**Preview flow:** `previewReport(reportType)` → show `#loadingModal` → AJAX POST `ReportGenerator/Preview` → inject HTML → `#previewModal`. **No AJAX timeout**; hang leaves spinner forever.

**Generate flow:** `generateReport(reportType, format)` default format **`pdf`** → POST `ReportGenerator/GenerateDirect` → `ReportService.GenerateReportByType` → for `format=="pdf"`: `pdfGenerator` returns **HTML string**, then `File.WriteAllText(filePath, html)` with extension `.pdf` (`ReportService.cs` ~42–45, ~69–72). Client navigates to `Download?fileName=…`. Preview footer PDF uses **html2pdf.js** (~299), not server PDF.

**#13 modals:** `#loadingModal` / `#previewModal` still **inline under `.app-page`** (~128–198), **not** `@section Modals`. `_Layout.cshtml` has **no** `RenderSection("Modals")` (only Scripts). `.app-page` animation stacking can trap Bootstrap modal/backdrop; hide can fail.

---

## How to reproduce

1. Admin → Insights → Reports.
2. **Preview** Candidates → observe loading modal → preview content. Close; check backdrop/stuck “Processing”.
3. **Generate** Candidates (list button, default) → download/open file named `candidate_*.pdf` → inspect: often HTML, not binary PDF.
4. Optional: preview → DOWNLOAD NOW as PDF → client html2pdf path.
5. Optional: Generate with CSV format → real CSV.

---

## Intended fix (safer approach; constraints)

**A. Honest Generate / real PDF (pick minimal viable)**

1. **Label / format honesty (smallest):** if server still emits HTML, use `.html` extension (or Content-Type text/html) and label the button “Download HTML”, **or**
2. **Server PDF:** replace HTML-written-as-`.pdf` with a real PDF library path, **or**
3. **Align with preview:** list Generate(pdf) delegates to the same client html2pdf approach (document tradeoffs), and keep CSV server-side.

Do not leave `.pdf` files that are raw HTML without fixing label or converter.

**B. #13 modal hardening**

1. Move `#loadingModal` / `#previewModal` to body end — either add `@section Modals` + `RenderSection("Modals", false)` in `_Layout`, or render modals outside `.app-page`.
2. Add AJAX **timeout** + error toast; always hide loading modal on fail/complete.
3. On hide: ensure backdrop cleanup (known Bootstrap + stacking-context issue).

**C.** Leave stub `ReportsController.GenerateDirect` alone unless something still POSTs it — UI must stay on `ReportGenerator`.

**Do not open a PR.**

---

## Files to inspect

- `HR.Web/Views/Reports/Index.cshtml` (`previewReport` ~213, `generateReport` ~321, modals ~128–198, html2pdf ~201/299)
- `HR.Web/Controllers/ReportGeneratorController.cs`
- `HR.Web/Services/ReportService.cs` (`GenerateReportByType`, `GenerateTypedReport`, `GenerateCandidatePDF`)
- `HR.Web/Views/Shared/_Layout.cshtml` (RenderSection)
- `HR.Web/Controllers/ReportsController.cs` (stub — awareness only)

---

## Success criteria

- Generate “PDF” either is a real PDF **or** is clearly labeled/served as HTML — no silent HTML-as-`.pdf`.
- CSV Generate still works.
- Preview: loading modal always dismisses; no permanent stuck “Processing Report”; backdrop clears on close.
- AJAX failure surfaces a toast/message within timeout.

---

## Cursor: verify and reason

Confirm local `GenerateReportByType` still `File.WriteAllText` HTML into `.pdf`. Confirm modals still under `.app-page` and layout lacks Modals section. Hypothesis: #13 = stacking context + no timeout; Generate = HTML generator mislabeled as PDF. Prefer minimal honesty fix or one real PDF path — do not rewrite the whole Reports module. Local may already have partial #13 work — verify. **Do not open a PR.**
