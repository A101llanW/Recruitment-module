using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using HR.Web.Filters;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    /// <summary>
    /// Interview candidate email workflow (template vs scratch, previews, CC).
    /// </summary>
    public partial class InterviewsController
    {
        private sealed class InterviewCandidateEmailContent
        {
            public string Subject { get; set; }
            public string BodyHtml { get; set; }
        }

        private sealed class InterviewComposeValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }

        private void PopulateInterviewEmailCcForIndexView()
        {
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

        private static int[] NormalizePostedIdArray(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return new int[0];
            }

            return ids.Where(id => id > 0).ToArray();
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

        private static string ValidateCustomEmailSubject(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                return "Please enter an email subject before sending.";
            }

            if (subject.Trim().Length > 255)
            {
                return "Subject is too long. Maximum length is 255 characters.";
            }

            return null;
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

        private static InterviewComposeValidationResult ValidateInterviewComposeInput(string composeMode, string subject, string body)
        {
            var useScratch = string.Equals(composeMode, "scratch", StringComparison.OrdinalIgnoreCase);
            var useTemplateEdit = string.Equals(composeMode, "template_edit", StringComparison.OrdinalIgnoreCase);

            if (useScratch)
            {
                var subjectError = ValidateCustomEmailSubject(subject);
                if (!string.IsNullOrWhiteSpace(subjectError))
                {
                    return InvalidInterviewCompose(subjectError);
                }

                var bodyError = ValidateRichEmailBody(body, 20000);
                if (!string.IsNullOrWhiteSpace(bodyError))
                {
                    return InvalidInterviewCompose(bodyError);
                }
            }
            else if (useTemplateEdit)
            {
                var subjectError = ValidateCustomEmailSubject(subject);
                if (!string.IsNullOrWhiteSpace(subjectError))
                {
                    return InvalidInterviewCompose(subjectError);
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    return InvalidInterviewCompose("Please enter email body content before sending.");
                }

                if (body.Trim().Length > 20000)
                {
                    return InvalidInterviewCompose("Edited template body is too long. Maximum length is 20000 characters.");
                }
            }
            else
            {
                return InvalidInterviewCompose("Invalid compose mode.");
            }

            return new InterviewComposeValidationResult { IsValid = true };
        }

        private static InterviewComposeValidationResult InvalidInterviewCompose(string message)
        {
            return new InterviewComposeValidationResult { IsValid = false, ErrorMessage = message };
        }

        private EmailTemplateCatalog.RenderedTemplate RenderInterviewCandidateTemplate(
            Company company,
            string candidateFullName,
            string positionTitle,
            DateTime scheduledAt,
            string mode,
            string templateKey,
            string customMessageHtml)
        {
            var key = EmailTemplateCatalog.NormalizeTemplateKey(templateKey);
            if (!string.Equals(key, EmailTemplateCatalog.InterviewCandidateStandard, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, EmailTemplateCatalog.InterviewCandidateReminder, StringComparison.OrdinalIgnoreCase))
            {
                key = EmailTemplateCatalog.InterviewCandidateStandard;
            }

            var positionDisplay = string.IsNullOrWhiteSpace(positionTitle) ? "the position" : positionTitle.Trim();
            var companyName = company != null && !string.IsNullOrWhiteSpace(company.Name)
                ? company.Name.Trim()
                : "Recruitment Team";
            var candidateNameSafe = string.IsNullOrWhiteSpace(candidateFullName) ? "Candidate" : candidateFullName.Trim();
            var modeDisplay = string.IsNullOrWhiteSpace(mode) ? "Interview" : mode.Trim();
            var customBlock = customMessageHtml ?? string.Empty;

            return _emailTemplateService.Render(
                key,
                new Dictionary<string, string>
                {
                    { "CandidateName", HttpUtility.HtmlEncode(candidateNameSafe) },
                    { "PositionTitle", HttpUtility.HtmlEncode(positionDisplay) },
                    { "InterviewDateTime", HttpUtility.HtmlEncode(scheduledAt.ToString("f")) },
                    { "InterviewMode", HttpUtility.HtmlEncode(modeDisplay) },
                    { "CompanyName", HttpUtility.HtmlEncode(companyName) },
                    { "CustomMessageBlock", customBlock }
                },
                company != null ? (int?)company.Id : null);
        }

        private InterviewCandidateEmailContent BuildInterviewCandidateEmailContent(
            Company company,
            string candidateFullName,
            string positionTitle,
            DateTime scheduledAt,
            string mode,
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
                return new InterviewCandidateEmailContent
                {
                    Subject = safeSubject,
                    BodyHtml = WrapCandidateEmailDocument(innerHtml)
                };
            }

            var rendered = RenderInterviewCandidateTemplate(
                company,
                candidateFullName,
                positionTitle,
                scheduledAt,
                mode,
                templateKey,
                customMessageHtml: string.Empty);

            return new InterviewCandidateEmailContent
            {
                Subject = rendered.Subject,
                BodyHtml = WrapCandidateEmailDocument(rendered.BodyHtml)
            };
        }

        private List<string> ResolveInterviewCcRecipients(
            int? companyId,
            string recipientEmail,
            bool includePanelistCc,
            bool includeHrCc,
            int[] selectedPanelistIds,
            int[] selectedHrCcIds,
            bool requireRecipientsWhenToggled)
        {
            if (!includePanelistCc && !includeHrCc)
            {
                return null;
            }

            if (!companyId.HasValue)
            {
                return null;
            }

            var ccRecipients = CandidateEmailCcHelper.BuildMergedCandidateCc(
                _uow,
                companyId.Value,
                includePanelistCc,
                selectedPanelistIds,
                includeHrCc,
                selectedHrCcIds,
                recipientEmail);

            if (requireRecipientsWhenToggled && (ccRecipients == null || !ccRecipients.Any()))
            {
                return new List<string>();
            }

            return ccRecipients;
        }

        private ActionResult RedirectWithInterviewEmailError(string message)
        {
            TempData["InterviewEmailError"] = message;
            return RedirectToAction("Index");
        }

        [HttpGet]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult GetInterviewCandidateTemplatePreview(int applicationId, string templateKey)
        {
            var application = LoadInterviewApplication(applicationId);
            if (application == null)
            {
                return Json(new { success = false, message = "Application not found." }, JsonRequestBehavior.AllowGet);
            }

            var accessDenied = ValidateInterviewApplicationAccess(application);
            if (accessDenied != null)
            {
                return Json(new { success = false, message = "Access denied." }, JsonRequestBehavior.AllowGet);
            }

            var interview = GetLatestInterviewForApplication(applicationId);
            if (interview == null)
            {
                return Json(new { success = false, message = "This candidate has no interview scheduled yet." }, JsonRequestBehavior.AllowGet);
            }

            var positionTitle = application.Position != null ? application.Position.Title : "the position";
            var company = application.CompanyId.HasValue ? _uow.Companies.Get(application.CompanyId.Value) : null;
            var rendered = RenderInterviewCandidateTemplate(
                company,
                application.Applicant != null ? application.Applicant.FullName : null,
                positionTitle,
                interview.ScheduledAt,
                interview.Mode,
                templateKey,
                customMessageHtml: null);

            return Json(
                new
                {
                    success = true,
                    templateKey = EmailTemplateCatalog.NormalizeTemplateKey(templateKey),
                    subject = rendered.Subject,
                    bodyHtml = rendered.BodyHtml,
                    candidateName = application.Applicant != null ? application.Applicant.FullName : "Candidate",
                    positionTitle = string.IsNullOrWhiteSpace(positionTitle) ? "this position" : positionTitle
                },
                JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult GetInterviewCandidatesBulkTemplatePreview(string templateKey)
        {
            var scheduledInterviews = GetManagementInterviews()
                .OrderByDescending(i => i.ScheduledAt)
                .ToList()
                .GroupBy(i => i.ApplicationId)
                .Select(g => g.First())
                .ToList();

            if (!scheduledInterviews.Any())
            {
                return Json(new { success = false, message = "No scheduled interviews found." }, JsonRequestBehavior.AllowGet);
            }

            var previewInterview = scheduledInterviews
                .FirstOrDefault(i => i.Application != null &&
                                     i.Application.Applicant != null &&
                                     !string.IsNullOrWhiteSpace(i.Application.Applicant.FullName))
                ?? scheduledInterviews.First();

            var application = previewInterview.Application;
            var positionTitle = application != null && application.Position != null ? application.Position.Title : "the position";
            var company = application != null && application.CompanyId.HasValue
                ? _uow.Companies.Get(application.CompanyId.Value)
                : null;

            var rendered = RenderInterviewCandidateTemplate(
                company,
                application != null && application.Applicant != null ? application.Applicant.FullName : null,
                positionTitle,
                previewInterview.ScheduledAt,
                previewInterview.Mode,
                templateKey,
                customMessageHtml: null);

            var recipientCount = scheduledInterviews
                .Count(i => i.Application != null &&
                            i.Application.Applicant != null &&
                            !string.IsNullOrWhiteSpace(i.Application.Applicant.Email));

            return Json(
                new
                {
                    success = true,
                    templateKey = EmailTemplateCatalog.NormalizeTemplateKey(templateKey),
                    subject = rendered.Subject,
                    bodyHtml = rendered.BodyHtml,
                    recipientCount = recipientCount
                },
                JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public async Task<ActionResult> SendInterviewCandidateEmail(CandidateEmailSendForm form)
        {
            form = form ?? new CandidateEmailSendForm();
            var applicationId = form.ApplicationId;
            var subject = form.Subject;
            var body = form.Body;
            var composeMode = form.ComposeMode;
            var templateKey = form.TemplateKey;
            var includePanelistCc = form.IncludePanelistCc;
            var includeHrCc = form.IncludeHrCc;
            var selectedPanelistIds = NormalizePostedIdArray(form.SelectedPanelistIds);
            var selectedHrCcIds = NormalizePostedIdArray(form.SelectedHrCcIds);

            var application = LoadInterviewApplication(applicationId);
            if (application == null)
            {
                return HttpNotFound();
            }

            var accessDenied = ValidateInterviewApplicationAccess(application);
            if (accessDenied != null)
            {
                return accessDenied;
            }

            var composeValidation = ValidateInterviewComposeInput(composeMode, subject, body);
            if (!composeValidation.IsValid)
            {
                return RedirectWithInterviewEmailError(composeValidation.ErrorMessage);
            }

            var interview = GetLatestInterviewForApplication(applicationId);
            if (interview == null)
            {
                return RedirectForMissingInterview(application, applicationId, body);
            }

            var recipientEmail = application.Applicant != null ? application.Applicant.Email : null;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return RedirectWithInterviewEmailError("Candidate has no email address on file.");
            }

            var positionTitle = application.Position != null ? application.Position.Title : "the position";
            var company = application.CompanyId.HasValue ? _uow.Companies.Get(application.CompanyId.Value) : null;
            var emailContent = BuildInterviewCandidateEmailContent(
                company,
                application.Applicant != null ? application.Applicant.FullName : null,
                positionTitle,
                interview.ScheduledAt,
                interview.Mode,
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
                return RedirectWithInterviewEmailError(ccValidation);
            }

            var ccRecipients = ResolveInterviewCcRecipients(
                application.CompanyId,
                recipientEmail.Trim(),
                includePanelistCc,
                includeHrCc,
                selectedPanelistIds,
                selectedHrCcIds,
                requireRecipientsWhenToggled: true);
            if (ccRecipients != null && !ccRecipients.Any())
            {
                return RedirectWithInterviewEmailError("No CC recipients could be resolved. Check selected addresses.");
            }

            await _email.SendAsync(recipientEmail.Trim(), emailContent.Subject, emailContent.BodyHtml, ccRecipients, application.CompanyId);

            Session.Remove(GetPendingInterviewEmailSessionKey(applicationId));
            TempData["InterviewEmailSuccess"] = string.Format(
                "Email sent to {0}.",
                application.Applicant != null && !string.IsNullOrWhiteSpace(application.Applicant.FullName)
                    ? application.Applicant.FullName
                    : recipientEmail.Trim());

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public async Task<ActionResult> SendInterviewCandidatesBatchEmail(CandidateEmailSendForm form)
        {
            form = form ?? new CandidateEmailSendForm();
            var subject = form.Subject;
            var body = form.Body;
            var composeMode = form.ComposeMode;
            var templateKey = form.TemplateKey;
            var includePanelistCc = form.IncludePanelistCc;
            var includeHrCc = form.IncludeHrCc;
            var selectedPanelistIds = NormalizePostedIdArray(form.SelectedPanelistIds);
            var selectedHrCcIds = NormalizePostedIdArray(form.SelectedHrCcIds);

            var composeValidation = ValidateInterviewComposeInput(composeMode, subject, body);
            if (!composeValidation.IsValid)
            {
                return RedirectWithInterviewEmailError(composeValidation.ErrorMessage);
            }

            var scheduledInterviews = GetManagementInterviews()
                .OrderByDescending(i => i.ScheduledAt)
                .ToList()
                .GroupBy(i => i.ApplicationId)
                .Select(g => g.First())
                .ToList();

            if (!scheduledInterviews.Any())
            {
                return RedirectWithInterviewEmailError("No scheduled interviews found for batch email.");
            }

            var interviewRecipients = scheduledInterviews
                .Where(i => i.Application != null &&
                            i.Application.Applicant != null &&
                            !string.IsNullOrWhiteSpace(i.Application.Applicant.Email))
                .ToList();

            if (!interviewRecipients.Any())
            {
                return RedirectWithInterviewEmailError("No candidate email addresses found for scheduled interviews.");
            }

            var ccValidation = CandidateEmailCcHelper.ValidateCcToggles(
                includePanelistCc,
                selectedPanelistIds,
                includeHrCc,
                selectedHrCcIds);
            if (!string.IsNullOrEmpty(ccValidation))
            {
                return RedirectWithInterviewEmailError(ccValidation);
            }

            var emailTasks = interviewRecipients.Select(interview =>
            {
                var application = interview.Application;
                var applicant = application.Applicant;
                var recipientEmail = applicant.Email.Trim();
                var positionTitle = application.Position != null ? application.Position.Title : "the position";
                var company = application.CompanyId.HasValue ? _uow.Companies.Get(application.CompanyId.Value) : null;
                var emailContent = BuildInterviewCandidateEmailContent(
                    company,
                    applicant.FullName,
                    positionTitle,
                    interview.ScheduledAt,
                    interview.Mode,
                    composeMode,
                    templateKey,
                    subject,
                    body);

                var ccRecipients = ResolveInterviewCcRecipients(
                    application.CompanyId,
                    recipientEmail,
                    includePanelistCc,
                    includeHrCc,
                    selectedPanelistIds,
                    selectedHrCcIds,
                    requireRecipientsWhenToggled: false);

                return _email.SendAsync(recipientEmail, emailContent.Subject, emailContent.BodyHtml, ccRecipients, application.CompanyId);
            }).ToList();

            foreach (var emailTask in emailTasks)
            {
                await emailTask;
            }

            TempData["InterviewEmailSuccess"] = string.Format(
                "Batch email sent to {0} candidate{1} with scheduled interviews.",
                interviewRecipients.Count,
                interviewRecipients.Count == 1 ? string.Empty : "s");
            return RedirectToAction("Index");
        }
    }
}
