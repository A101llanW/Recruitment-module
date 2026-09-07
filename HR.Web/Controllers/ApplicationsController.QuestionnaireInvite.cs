using System;
using System.Collections.Generic;
using System.Data.Entity;
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
    public partial class ApplicationsController
    {
        [HttpPost]
        [TenantAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> InviteNextQuestionnaireStage(int applicationId)
        {
            var actor = GetCurrentUser();
            if (actor == null)
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var rolePermissionService = new RolePermissionService();
            if (!rolePermissionService.IsFullCompanyAdmin(actor) && !_tenantService.IsActualSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            if (applicationId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid application.";
                return RedirectToApplicationsIndex();
            }

            var application = _uow.Context.Applications
                .Include(a => a.Position)
                .Include(a => a.Applicant)
                .FirstOrDefault(a => a.Id == applicationId);

            if (application == null)
            {
                TempData["ErrorMessage"] = "Application not found.";
                return RedirectToApplicationsIndex();
            }

            var position = application.Position;
            if (position == null)
            {
                TempData["ErrorMessage"] = "Position not found.";
                return RedirectToApplicationsIndex();
            }

            var tenantValidationResult = ValidatePositionTenantAccess(position, "Access Denied");
            if (tenantValidationResult != null)
            {
                return tenantValidationResult;
            }

            var maxStages = Math.Max(1, position.QuestionnaireStageCount);
            if (maxStages <= 1)
            {
                TempData["ErrorMessage"] = "This position does not use multiple questionnaire stages.";
                return RedirectToApplicationsIndex();
            }

            if (application.LastCompletedQuestionnaireStage <= 0)
            {
                TempData["ErrorMessage"] = "The candidate has not completed stage 1 yet.";
                return RedirectToApplicationsIndex();
            }

            if (application.LastCompletedQuestionnaireStage >= maxStages)
            {
                TempData["ErrorMessage"] = "All questionnaire stages are already complete.";
                return RedirectToApplicationsIndex();
            }

            if (application.PendingQuestionnaireStage.HasValue)
            {
                TempData["ErrorMessage"] = "The candidate already has an open questionnaire stage.";
                return RedirectToApplicationsIndex();
            }

            var nextStage = application.LastCompletedQuestionnaireStage + 1;
            if (application.Score.HasValue)
            {
                application.LastQuestionnaireScore = application.Score;
            }

            application.PendingQuestionnaireStage = nextStage;
            application.QuestionnaireInvitedOn = DateTime.UtcNow;
            _uow.Applications.Update(application);
            _uow.Complete();

            var openedMsg = string.Format(
                "Questionnaire stage {0} has been opened for the candidate.",
                nextStage);

            var recipientEmail = application.Applicant != null ? application.Applicant.Email : null;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                TempData["SuccessMessage"] = openedMsg + " No invitation email was sent because the applicant has no email address on file.";
                return RedirectToApplicationsIndex();
            }

            var company = application.CompanyId.HasValue ? _uow.Companies.Get(application.CompanyId.Value) : null;
            var companyName = company != null && !string.IsNullOrWhiteSpace(company.Name)
                ? company.Name.Trim()
                : "Recruitment Team";
            var candidateName = application.Applicant != null && !string.IsNullOrWhiteSpace(application.Applicant.FullName)
                ? application.Applicant.FullName.Trim()
                : "Candidate";
            var positionTitle = string.IsNullOrWhiteSpace(position.Title) ? "this position" : position.Title.Trim();

            var questionnairePath = BuildQuestionnaireInvitationPath(company, position.Id);
            var baseUrl = ExternalUrlHelper.GetBaseUri(Request);
            var baseUri = EnsureQuestionnaireInviteBaseUrl(baseUrl);
            var stageLink = new Uri(baseUri, questionnairePath.TrimStart('/')).ToString();

            try
            {
                var encodedLink = HttpUtility.HtmlEncode(stageLink);
                var rendered = _emailTemplateService.Render(
                    EmailTemplateCatalog.SecondaryStageInvitation,
                    new Dictionary<string, string>
                    {
                        { "CandidateName", HttpUtility.HtmlEncode(candidateName) },
                        { "PositionTitle", HttpUtility.HtmlEncode(positionTitle) },
                        { "CompanyName", HttpUtility.HtmlEncode(companyName) },
                        { EmailTemplateCatalog.QuestionnaireStageLinkToken, encodedLink },
                        { "CustomMessageBlock", string.Empty }
                    },
                    application.CompanyId);

                if (rendered == null)
                {
                    TempData["ApplicationEmailError"] = openedMsg + " However, the invitation email template could not be rendered.";
                    return RedirectToApplicationsIndex();
                }

                var emailContent = rendered;
                await _email.SendCriticalAsync(
                    recipientEmail.Trim(),
                    emailContent.Subject ?? "Questionnaire invitation",
                    WrapCandidateEmailDocument(emailContent.BodyHtml ?? string.Empty));

                TempData["SuccessMessage"] = openedMsg + " Invitation email sent.";
            }
            catch (Exception ex)
            {
                TempData["ApplicationEmailError"] = string.Format(
                    "{0} However, the invitation email could not be sent ({1}).",
                    openedMsg,
                    ex.Message);
            }

            return RedirectToApplicationsIndex();
        }

        private string BuildQuestionnaireInvitationPath(Company company, int positionId)
        {
            var slug = company != null && !string.IsNullOrWhiteSpace(company.Slug)
                ? company.Slug.Trim()
                : null;

            if (!string.IsNullOrEmpty(slug))
            {
                return Url.Action("Questionnaire", "Applications", new { tenant = slug, positionId = positionId }) ?? string.Empty;
            }

            return Url.Action("Questionnaire", "Applications", new { positionId = positionId }) ?? string.Empty;
        }

        private static Uri EnsureQuestionnaireInviteBaseUrl(Uri value)
        {
            if (value == null)
            {
                return new Uri("http://localhost/", UriKind.Absolute);
            }

            var text = value.ToString();
            return new Uri(text.EndsWith("/", StringComparison.Ordinal) ? text : text + "/", UriKind.Absolute);
        }
    }
}
