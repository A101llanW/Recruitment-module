using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using HR.Web.Filters;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    /// <summary>
    /// Failed-candidate email workflow (template vs scratch, previews, CC).
    /// </summary>
    public partial class ApplicationsController
    {
        private sealed class FailedCandidateEmailContent
        {
            public string Subject { get; set; }
            public string BodyHtml { get; set; }
        }

        private void PopulateFailedCandidateEmailApplicationIdsForIndexView()
        {
            var appsQuery = _uow.Context.Applications.AsQueryable();
            appsQuery = _tenantService.ApplyTenantFilter(appsQuery);
            ViewBag.FailedCandidateEmailApplicationIds = new HashSet<int>(
                appsQuery.Where(a => a.FailedCandidateEmailSentAt.HasValue).Select(a => a.Id).ToList());

            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue)
            {
                var tenantUsers = _uow.Users.GetAll().Where(u => u.CompanyId == companyId.Value).ToList();
                ViewBag.EmailCcPanelists = CandidateEmailCcHelper.GetPanelistUsersForCc(tenantUsers);
                ViewBag.EmailCcHrContacts = CandidateEmailCcHelper.GetActiveHrContacts(_uow.Context, companyId.Value);
            }
            else
            {
                ViewBag.EmailCcPanelists = new List<User>();
                ViewBag.EmailCcHrContacts = new List<CompanyHrCcEmail>();
            }
        }

        private static int ParsePostedInt32(string raw, int defaultValue)
        {
            int v;
            return int.TryParse(raw, out v) ? v : defaultValue;
        }

        private static bool ParsePostedCheckbox(NameValueCollection form, string key)
        {
            if (form == null)
            {
                return false;
            }

            var v = form[key];
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static int[] ParsePostedInt32Array(NameValueCollection form, string key)
        {
            if (form == null)
            {
                return new int[0];
            }

            var vals = form.GetValues(key);
            if (vals == null || vals.Length == 0)
            {
                return new int[0];
            }

            var list = new List<int>();
            foreach (var s in vals)
            {
                int id;
                if (int.TryParse(s, out id) && id > 0)
                {
                    list.Add(id);
                }
            }

            return list.ToArray();
        }

        private static string WrapCandidateEmailDocument(string innerHtml)
        {
            return string.Format(
                @"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""/></head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
{0}
</body>
</html>",
                innerHtml ?? string.Empty);
        }

        private static string FormatOptionalPlainNoteAsHtml(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return string.Empty;
            }

            var t = HttpUtility.HtmlEncode(note.Trim())
                .Replace("\r\n", "<br/>")
                .Replace("\n", "<br/>");
            return "<p>" + t + "</p>";
        }

        private EmailTemplateCatalog.RenderedTemplate RenderFailedCandidateTemplate(
            Company company,
            string candidateFullName,
            string positionTitle,
            string templateKey,
            string customMessage)
        {
            var key = EmailTemplateCatalog.NormalizeTemplateKey(templateKey);
            if (!string.Equals(key, EmailTemplateCatalog.FailedCandidateStandard, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, EmailTemplateCatalog.FailedCandidateNextSteps, StringComparison.OrdinalIgnoreCase))
            {
                key = EmailTemplateCatalog.FailedCandidateStandard;
            }

            var positionDisplay = string.IsNullOrWhiteSpace(positionTitle) ? "this position" : positionTitle.Trim();
            var companyName = company != null && !string.IsNullOrWhiteSpace(company.Name)
                ? company.Name.Trim()
                : "Recruitment Team";
            var candidateNameSafe = string.IsNullOrWhiteSpace(candidateFullName) ? "Candidate" : candidateFullName.Trim();
            var customBlock = string.IsNullOrWhiteSpace(customMessage) ? string.Empty : customMessage;

            return _emailTemplateService.Render(
                key,
                new Dictionary<string, string>
                {
                    { "CandidateName", HttpUtility.HtmlEncode(candidateNameSafe) },
                    { "PositionTitle", HttpUtility.HtmlEncode(positionDisplay) },
                    { "CompanyName", HttpUtility.HtmlEncode(companyName) },
                    { "CustomMessageBlock", customBlock }
                },
                company != null ? (int?)company.Id : null);
        }

        private FailedCandidateEmailContent BuildFailedCandidateEmailContent(
            Company company,
            string candidateFullName,
            string positionTitle,
            string composeMode,
            string templateKey,
            string subject,
            string body)
        {
            var useScratch = string.Equals(composeMode, "scratch", StringComparison.OrdinalIgnoreCase);
            var useTemplateEdit = string.Equals(composeMode, "template_edit", StringComparison.OrdinalIgnoreCase);

            if (useScratch || useTemplateEdit)
            {
                var safeSubject = (subject ?? string.Empty).Trim();
                var innerHtml = EmailBodyHtmlSanitizer.Sanitize(body ?? string.Empty);
                return new FailedCandidateEmailContent
                {
                    Subject = safeSubject,
                    BodyHtml = WrapCandidateEmailDocument(innerHtml)
                };
            }

            var rendered = RenderFailedCandidateTemplate(
                company,
                candidateFullName,
                positionTitle,
                templateKey,
                FormatOptionalPlainNoteAsHtml(body));

            return new FailedCandidateEmailContent
            {
                Subject = rendered.Subject,
                BodyHtml = WrapCandidateEmailDocument(rendered.BodyHtml)
            };
        }

        private static string ValidateRichEmailBody(string body, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "Please enter an email body before sending.";
            }

            if (body.Trim().Length > maxLen)
            {
                return string.Format("Message is too long. Maximum length is {0} characters.", maxLen);
            }

            return null;
        }

        [HttpGet]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult GetFailedCandidateTemplatePreview(int applicationId, string templateKey)
        {
            var app = _uow.Applications.GetAll(a => a.Applicant, a => a.Position)
                .FirstOrDefault(a => a.Id == applicationId);
            if (app == null)
            {
                return Json(new { success = false, message = "Application not found." }, JsonRequestBehavior.AllowGet);
            }

            var tenantValidationResult = ValidateApplicationTenantAccess(app, "Access Denied");
            if (tenantValidationResult != null)
            {
                return Json(new { success = false, message = "Access denied." }, JsonRequestBehavior.AllowGet);
            }

            var position = app.Position ?? _uow.Positions.Get(app.PositionId);
            if (position == null)
            {
                return Json(new { success = false, message = "Position not found for this application." }, JsonRequestBehavior.AllowGet);
            }

            if (!IsApplicationBelowPassMark(app, position))
            {
                return Json(new { success = false, message = "Template preview is only available for failed candidates." }, JsonRequestBehavior.AllowGet);
            }

            var company = app.CompanyId.HasValue ? _uow.Companies.Get(app.CompanyId.Value) : null;
            var rendered = RenderFailedCandidateTemplate(
                company,
                app.Applicant != null ? app.Applicant.FullName : null,
                position.Title,
                templateKey,
                customMessage: null);

            return Json(
                new
                {
                    success = true,
                    templateKey = EmailTemplateCatalog.NormalizeTemplateKey(templateKey),
                    subject = rendered.Subject,
                    bodyHtml = rendered.BodyHtml,
                    candidateName = app.Applicant != null ? app.Applicant.FullName : "Candidate",
                    positionTitle = string.IsNullOrWhiteSpace(position.Title) ? "this position" : position.Title
                },
                JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public async Task<ActionResult> SendFailedCandidateEmail()
        {
            var form = Request.Unvalidated.Form;
            var applicationId = ParsePostedInt32(form["applicationId"], 0);
            var subject = form["subject"];
            var body = form["body"];
            var composeMode = form["composeMode"];
            var templateKey = form["templateKey"];
            var includePanelistCc = ParsePostedCheckbox(form, "includePanelistCc");
            var includeHrCc = ParsePostedCheckbox(form, "includeHrCc");
            var selectedPanelistIds = ParsePostedInt32Array(form, "selectedPanelistIds");
            var selectedHrCcIds = ParsePostedInt32Array(form, "selectedHrCcIds");

            var app = _uow.Applications.GetAll(a => a.Applicant, a => a.Position)
                .FirstOrDefault(a => a.Id == applicationId);
            if (app == null)
            {
                return HttpNotFound();
            }

            var tenantValidationResult = ValidateApplicationTenantAccess(app, "Access Denied");
            if (tenantValidationResult != null)
            {
                return tenantValidationResult;
            }

            var useScratch = string.Equals(composeMode, "scratch", StringComparison.OrdinalIgnoreCase);
            var useTemplateEdit = string.Equals(composeMode, "template_edit", StringComparison.OrdinalIgnoreCase);
            if (useScratch)
            {
                var subjectValidationError = ValidateCustomEmailSubject(subject);
                if (!string.IsNullOrWhiteSpace(subjectValidationError))
                {
                    TempData["ApplicationEmailError"] = subjectValidationError;
                    return RedirectToAction("Index");
                }

                var richBodyError = ValidateRichEmailBody(body, 20000);
                if (!string.IsNullOrWhiteSpace(richBodyError))
                {
                    TempData["ApplicationEmailError"] = richBodyError;
                    return RedirectToAction("Index");
                }
            }
            else if (useTemplateEdit)
            {
                var subjectValidationError = ValidateCustomEmailSubject(subject);
                if (!string.IsNullOrWhiteSpace(subjectValidationError))
                {
                    TempData["ApplicationEmailError"] = subjectValidationError;
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    TempData["ApplicationEmailError"] = "Please enter email body content before sending.";
                    return RedirectToAction("Index");
                }

                if (body.Trim().Length > 20000)
                {
                    TempData["ApplicationEmailError"] = "Edited template body is too long. Maximum length is 20000 characters.";
                    return RedirectToAction("Index");
                }
            }

            if (!useScratch && !useTemplateEdit && !string.IsNullOrWhiteSpace(body) && body.Trim().Length > 4000)
            {
                TempData["ApplicationEmailError"] = "Custom template note is too long. Maximum length is 4000 characters.";
                return RedirectToAction("Index");
            }

            var position = app.Position ?? _uow.Positions.Get(app.PositionId);
            if (position == null)
            {
                TempData["ApplicationEmailError"] = "Position could not be found for this application.";
                return RedirectToAction("Index");
            }

            if (!IsApplicationBelowPassMark(app, position))
            {
                TempData["ApplicationEmailError"] = "Email can only be sent from the failed-candidates list.";
                return RedirectToAction("Index");
            }

            var recipientEmail = app.Applicant != null ? app.Applicant.Email : null;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                TempData["ApplicationEmailError"] = "Candidate has no email address on file.";
                return RedirectToAction("Index");
            }

            var company = app.CompanyId.HasValue ? _uow.Companies.Get(app.CompanyId.Value) : null;
            var emailContent = BuildFailedCandidateEmailContent(
                company,
                app.Applicant != null ? app.Applicant.FullName : null,
                position.Title,
                composeMode,
                templateKey,
                subject,
                body);

            var ccValidation = CandidateEmailCcHelper.ValidateCcToggles(
                includePanelistCc,
                selectedPanelistIds,
                includeHrCc,
                selectedHrCcIds);
            if (!string.IsNullOrEmpty(ccValidation))
            {
                TempData["ApplicationEmailError"] = ccValidation;
                return RedirectToAction("Index");
            }

            List<string> ccRecipients = null;
            if ((includePanelistCc || includeHrCc) && app.CompanyId.HasValue)
            {
                ccRecipients = CandidateEmailCcHelper.BuildMergedCandidateCc(
                    _uow,
                    app.CompanyId.Value,
                    includePanelistCc,
                    selectedPanelistIds,
                    includeHrCc,
                    selectedHrCcIds,
                    recipientEmail.Trim());
                if (ccRecipients == null || !ccRecipients.Any())
                {
                    TempData["ApplicationEmailError"] = "No CC recipients could be resolved. Check selected addresses.";
                    return RedirectToAction("Index");
                }
            }

            await _email.SendAsync(recipientEmail.Trim(), emailContent.Subject, emailContent.BodyHtml, ccRecipients);

            app.FailedCandidateEmailSentAt = DateTime.UtcNow;
            _uow.Applications.Update(app);
            _uow.Complete();

            TempData["ApplicationEmailSuccess"] = string.Format(
                "Email sent to {0}.",
                app.Applicant != null ? app.Applicant.FullName : recipientEmail.Trim());
            return RedirectToAction("Index");
        }

        [HttpGet]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult GetFailedCandidatesBulkTemplatePreview(int positionId, string templateKey)
        {
            if (positionId <= 0)
            {
                return Json(new { success = false, message = "Invalid position selected." }, JsonRequestBehavior.AllowGet);
            }

            var position = _uow.Positions.Get(positionId);
            if (position == null)
            {
                return Json(new { success = false, message = "Position not found." }, JsonRequestBehavior.AllowGet);
            }

            var tenantValidationResult = ValidatePositionTenantAccess(position, "Access Denied");
            if (tenantValidationResult != null)
            {
                return Json(new { success = false, message = "Access denied." }, JsonRequestBehavior.AllowGet);
            }

            var failedApplications = _uow.Applications.GetAll(a => a.Applicant)
                .Where(a => a.PositionId == positionId && (a.Score ?? 0m) < position.PassMark)
                .ToList();
            if (!failedApplications.Any())
            {
                return Json(new { success = false, message = "No failed candidates found for this position." }, JsonRequestBehavior.AllowGet);
            }

            var previewRecipient = failedApplications
                .FirstOrDefault(a => a.Applicant != null && !string.IsNullOrWhiteSpace(a.Applicant.FullName))
                ?? failedApplications.First();

            var company = position.CompanyId.HasValue ? _uow.Companies.Get(position.CompanyId.Value) : null;
            var rendered = RenderFailedCandidateTemplate(
                company,
                previewRecipient.Applicant != null ? previewRecipient.Applicant.FullName : null,
                position.Title,
                templateKey,
                customMessage: null);

            return Json(
                new
                {
                    success = true,
                    templateKey = EmailTemplateCatalog.NormalizeTemplateKey(templateKey),
                    subject = rendered.Subject,
                    bodyHtml = rendered.BodyHtml,
                    failedCandidateCount = failedApplications.Count
                },
                JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public async Task<ActionResult> SendFailedCandidatesBulkEmail()
        {
            var form = Request.Unvalidated.Form;
            var positionId = ParsePostedInt32(form["positionId"], 0);
            var subject = form["subject"];
            var body = form["body"];
            var composeMode = form["composeMode"];
            var templateKey = form["templateKey"];
            var includePanelistCc = ParsePostedCheckbox(form, "includePanelistCc");
            var includeHrCc = ParsePostedCheckbox(form, "includeHrCc");
            var selectedPanelistIds = ParsePostedInt32Array(form, "selectedPanelistIds");
            var selectedHrCcIds = ParsePostedInt32Array(form, "selectedHrCcIds");

            if (positionId <= 0)
            {
                TempData["ApplicationEmailError"] = "Invalid position selected.";
                return RedirectToAction("Index");
            }

            var position = _uow.Positions.Get(positionId);
            if (position == null)
            {
                return HttpNotFound();
            }

            var tenantValidationResult = ValidatePositionTenantAccess(position, "Access Denied");
            if (tenantValidationResult != null)
            {
                return tenantValidationResult;
            }

            var useScratch = string.Equals(composeMode, "scratch", StringComparison.OrdinalIgnoreCase);
            var useTemplateEdit = string.Equals(composeMode, "template_edit", StringComparison.OrdinalIgnoreCase);
            if (useScratch)
            {
                var subjectValidationError = ValidateCustomEmailSubject(subject);
                if (!string.IsNullOrWhiteSpace(subjectValidationError))
                {
                    TempData["ApplicationEmailError"] = subjectValidationError;
                    return RedirectToAction("Index");
                }

                var richBodyError = ValidateRichEmailBody(body, 20000);
                if (!string.IsNullOrWhiteSpace(richBodyError))
                {
                    TempData["ApplicationEmailError"] = richBodyError;
                    return RedirectToAction("Index");
                }
            }
            else if (useTemplateEdit)
            {
                var subjectValidationError = ValidateCustomEmailSubject(subject);
                if (!string.IsNullOrWhiteSpace(subjectValidationError))
                {
                    TempData["ApplicationEmailError"] = subjectValidationError;
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    TempData["ApplicationEmailError"] = "Please enter email body content before sending.";
                    return RedirectToAction("Index");
                }

                if (body.Trim().Length > 20000)
                {
                    TempData["ApplicationEmailError"] = "Edited template body is too long. Maximum length is 20000 characters.";
                    return RedirectToAction("Index");
                }
            }

            if (!useScratch && !useTemplateEdit && !string.IsNullOrWhiteSpace(body) && body.Trim().Length > 4000)
            {
                TempData["ApplicationEmailError"] = "Custom template note is too long. Maximum length is 4000 characters.";
                return RedirectToAction("Index");
            }

            var failedApplications = _uow.Applications.GetAll(a => a.Applicant)
                .Where(a => a.PositionId == positionId && (a.Score ?? 0m) < position.PassMark)
                .ToList();

            if (!failedApplications.Any())
            {
                TempData["ApplicationEmailError"] = "No failed candidates found for this position.";
                return RedirectToAction("Index");
            }

            var recipients = failedApplications
                .Where(a => a.Applicant != null && !string.IsNullOrWhiteSpace(a.Applicant.Email))
                .GroupBy(a => a.Applicant.Email.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (!recipients.Any())
            {
                TempData["ApplicationEmailError"] = "No valid candidate emails found among failed candidates.";
                return RedirectToAction("Index");
            }

            var company = position.CompanyId.HasValue ? _uow.Companies.Get(position.CompanyId.Value) : null;
            var ccValidation = CandidateEmailCcHelper.ValidateCcToggles(
                includePanelistCc,
                selectedPanelistIds,
                includeHrCc,
                selectedHrCcIds);
            if (!string.IsNullOrEmpty(ccValidation))
            {
                TempData["ApplicationEmailError"] = ccValidation;
                return RedirectToAction("Index");
            }

            var emailTasks = recipients.Select(r =>
            {
                var recipientEmail = r.Applicant.Email.Trim();
                var emailContent = BuildFailedCandidateEmailContent(
                    company,
                    r.Applicant != null ? r.Applicant.FullName : null,
                    position.Title,
                    composeMode,
                    templateKey,
                    subject,
                    body);

                List<string> ccRecipients = null;
                if ((includePanelistCc || includeHrCc) && position.CompanyId.HasValue)
                {
                    ccRecipients = CandidateEmailCcHelper.BuildMergedCandidateCc(
                        _uow,
                        position.CompanyId.Value,
                        includePanelistCc,
                        selectedPanelistIds,
                        includeHrCc,
                        selectedHrCcIds,
                        recipientEmail);
                }

                return _email.SendAsync(recipientEmail, emailContent.Subject, emailContent.BodyHtml, ccRecipients);
            }).ToList();

            await Task.WhenAll(emailTasks);

            var emailedAt = DateTime.UtcNow;
            foreach (var recipientApp in recipients)
            {
                recipientApp.FailedCandidateEmailSentAt = emailedAt;
                _uow.Applications.Update(recipientApp);
            }

            _uow.Complete();

            TempData["ApplicationEmailSuccess"] = string.Format(
                "Bulk email sent to {0} failed candidate{1} for {2}.",
                recipients.Count,
                recipients.Count == 1 ? string.Empty : "s",
                string.IsNullOrWhiteSpace(position.Title) ? "the selected position" : position.Title);

            return RedirectToAction("Index");
        }
    }
}
