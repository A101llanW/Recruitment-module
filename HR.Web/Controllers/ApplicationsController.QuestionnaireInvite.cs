using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    public partial class ApplicationsController
    {
        [HttpPost]
        [Authorize]
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
                return RedirectToAction("Index");
            }

            var application = _uow.Context.Applications
                .Include(a => a.Position)
                .Include(a => a.Applicant)
                .FirstOrDefault(a => a.Id == applicationId);

            if (application == null)
            {
                TempData["ErrorMessage"] = "Application not found.";
                return RedirectToAction("Index");
            }

            var position = application.Position;
            if (position == null)
            {
                TempData["ErrorMessage"] = "Position not found.";
                return RedirectToAction("Index");
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
                return RedirectToAction("Index");
            }

            if (application.LastCompletedQuestionnaireStage <= 0)
            {
                TempData["ErrorMessage"] = "The candidate has not completed stage 1 yet.";
                return RedirectToAction("Index");
            }

            if (application.LastCompletedQuestionnaireStage >= maxStages)
            {
                TempData["ErrorMessage"] = "All questionnaire stages are already complete.";
                return RedirectToAction("Index");
            }

            if (application.PendingQuestionnaireStage.HasValue)
            {
                TempData["ErrorMessage"] = "The candidate already has an open questionnaire stage.";
                return RedirectToAction("Index");
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

            TempData["SuccessMessage"] = string.Format(
                "Questionnaire stage {0} has been opened for the candidate.",
                nextStage);

            var recipientEmail = application.Applicant != null ? application.Applicant.Email : null;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                var openedMsg = string.Format(
                    "Questionnaire stage {0} has been opened for the candidate.",
                    nextStage);
                TempData["SuccessMessage"] = openedMsg + " No invitation email was sent because the applicant has no email address on file.";
                return RedirectToAction("Index");
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

                await _email.SendAsync(
                    recipientEmail.Trim(),
                    rendered.Subject,
                    WrapCandidateEmailDocument(rendered.BodyHtml));
            }
            catch (Exception ex)
            {
                TempData["ApplicationEmailError"] = string.Format(
                    "The stage was opened, but the invitation email could not be sent ({0}).",
                    ex.Message);
            }

            return RedirectToAction("Index");
        }

        private string BuildQuestionnaireInvitationPath(Company company, int positionId)
        {
            var slug = company != null && !string.IsNullOrWhiteSpace(company.Slug)
                ? company.Slug.Trim()
                : null;

            if (!string.IsNullOrEmpty(slug))
            {
                return Url.Action("Questionnaire", "Applications", new { tenant = slug, positionId = positionId });
            }

            return Url.Action("Questionnaire", "Applications", new { positionId = positionId });
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

        private static string EnsureQuestionnaireInviteBaseUrl(string value)
        {
            return EnsureQuestionnaireInviteBaseUrl(
                Uri.TryCreate(value, UriKind.Absolute, out var parsedUri) ? parsedUri : null).ToString();
        }
    }
}
