using System;
using System.Collections.Generic;
using System.Web;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    public partial class ApplicationsController
    {
        private EmailTemplateCatalog.RenderedTemplate RenderApplicationReceivedTemplate(
            Company company,
            string candidateFullName,
            string positionTitle)
        {
            var positionDisplay = string.IsNullOrWhiteSpace(positionTitle) ? "this position" : positionTitle.Trim();
            var companyName = company != null && !string.IsNullOrWhiteSpace(company.Name)
                ? company.Name.Trim()
                : "Recruitment Team";
            var candidateNameSafe = string.IsNullOrWhiteSpace(candidateFullName) ? "Candidate" : candidateFullName.Trim();

            return _emailTemplateService.Render(
                EmailTemplateCatalog.ApplicationReceivedStandard,
                new Dictionary<string, string>
                {
                    { "CandidateName", HttpUtility.HtmlEncode(candidateNameSafe) },
                    { "PositionTitle", HttpUtility.HtmlEncode(positionDisplay) },
                    { "CompanyName", HttpUtility.HtmlEncode(companyName) },
                    { "CustomMessageBlock", string.Empty }
                },
                company != null ? (int?)company.Id : null);
        }

        private void TrySendApplicationReceivedEmail(
            string recipientEmail,
            Company company,
            string candidateFullName,
            string positionTitle)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return;
            }

            var rendered = RenderApplicationReceivedTemplate(company, candidateFullName, positionTitle);
            if (rendered == null)
            {
                return;
            }

            var companyId = company != null ? (int?)company.Id : null;
            _email.SendAsync(
                recipientEmail.Trim(),
                rendered.Subject ?? "Application received",
                WrapCandidateEmailDocument(rendered.BodyHtml),
                companyId).GetAwaiter().GetResult();
        }

        private void TrySendApplicationReceivedEmailForApplication(
            Application application,
            Applicant applicant,
            Position position)
        {
            if (applicant == null || string.IsNullOrWhiteSpace(applicant.Email))
            {
                return;
            }

            Company company = null;
            if (position != null && position.CompanyId.HasValue)
            {
                company = _uow.Companies.Get(position.CompanyId.Value);
            }
            else if (application != null && application.CompanyId.HasValue)
            {
                company = _uow.Companies.Get(application.CompanyId.Value);
            }

            TrySendApplicationReceivedEmail(
                applicant.Email,
                company,
                applicant.FullName,
                position != null ? position.Title : null);
        }
    }
}
