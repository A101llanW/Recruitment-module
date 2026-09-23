using System;
using System.Collections.Generic;
using System.Web;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    public partial class InterviewsController
    {
        private EmailTemplateCatalog.RenderedTemplate RenderInterviewerAssignedTemplate(
            Company company,
            string interviewerName,
            string candidateFullName,
            string positionTitle,
            DateTime scheduledAt,
            string mode)
        {
            var positionDisplay = string.IsNullOrWhiteSpace(positionTitle) ? "the position" : positionTitle.Trim();
            var companyName = company != null && !string.IsNullOrWhiteSpace(company.Name)
                ? company.Name.Trim()
                : "Recruitment Team";
            var interviewerNameSafe = string.IsNullOrWhiteSpace(interviewerName) ? "Interviewer" : interviewerName.Trim();
            var candidateNameSafe = string.IsNullOrWhiteSpace(candidateFullName) ? "Candidate" : candidateFullName.Trim();
            var modeDisplay = string.IsNullOrWhiteSpace(mode) ? "Interview" : mode.Trim();

            return _emailTemplateService.Render(
                EmailTemplateCatalog.InterviewerAssignedStandard,
                new Dictionary<string, string>
                {
                    { "InterviewerName", HttpUtility.HtmlEncode(interviewerNameSafe) },
                    { "CandidateName", HttpUtility.HtmlEncode(candidateNameSafe) },
                    { "PositionTitle", HttpUtility.HtmlEncode(positionDisplay) },
                    { "InterviewDateTime", HttpUtility.HtmlEncode(scheduledAt.ToString("f")) },
                    { "InterviewMode", HttpUtility.HtmlEncode(modeDisplay) },
                    { "CompanyName", HttpUtility.HtmlEncode(companyName) },
                    { "CustomMessageBlock", string.Empty }
                },
                company != null ? (int?)company.Id : null);
        }

        private EmailSendResult TryNotifyInterviewerByEmail(
            string recipientEmail,
            Company company,
            string interviewerName,
            string candidateFullName,
            string positionTitle,
            DateTime scheduledAt,
            string mode)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return EmailSendResult.Skipped();
            }

            var rendered = RenderInterviewerAssignedTemplate(
                company,
                interviewerName,
                candidateFullName,
                positionTitle,
                scheduledAt,
                mode);
            if (rendered == null)
            {
                return EmailSendResult.Skipped();
            }

            return _email.TrySendAsync(
                recipientEmail.Trim(),
                rendered.Subject ?? "Interview assigned",
                WrapCandidateEmailDocument(rendered.BodyHtml),
                company != null ? (int?)company.Id : null).GetAwaiter().GetResult();
        }

        private EmailSendResult NotifyInterviewerOfBooking(int interviewerId, int interviewId, int applicationId, DateTime scheduledAt, string mode)
        {
            var interviewer = _uow.Users.Get(interviewerId);
            if (interviewer == null)
            {
                return EmailSendResult.Skipped();
            }

            var application = LoadInterviewApplication(applicationId);
            var company = ResolveInterviewNotificationCompany(application);
            var interviewerName = FormatInterviewerDisplayName(interviewer);
            var candidateName = application != null && application.Applicant != null
                ? application.Applicant.FullName
                : null;
            var positionTitle = application != null && application.Position != null
                ? application.Position.Title
                : null;

            return TryNotifyInterviewerByEmail(
                interviewer.Email,
                company,
                interviewerName,
                candidateName,
                positionTitle,
                scheduledAt,
                mode);
        }

        private EmailSendResult TryNotifyInterviewerAfterInterviewCreated(Interview interview)
        {
            if (interview == null)
            {
                return EmailSendResult.Skipped();
            }

            var interviewer = interview.InterviewerId > 0 ? _uow.Users.Get(interview.InterviewerId) : null;
            if (interviewer == null)
            {
                return EmailSendResult.Skipped();
            }

            var application = LoadInterviewApplication(interview.ApplicationId);
            var company = interview.CompanyId.HasValue
                ? _uow.Companies.Get(interview.CompanyId.Value)
                : ResolveInterviewNotificationCompany(application);
            var interviewerName = FormatInterviewerDisplayName(interviewer);
            var candidateName = application != null && application.Applicant != null
                ? application.Applicant.FullName
                : null;
            var positionTitle = application != null && application.Position != null
                ? application.Position.Title
                : null;

            return TryNotifyInterviewerByEmail(
                interviewer.Email,
                company,
                interviewerName,
                candidateName,
                positionTitle,
                interview.ScheduledAt,
                interview.Mode);
        }

        private Company ResolveInterviewNotificationCompany(Application application)
        {
            if (application == null || !application.CompanyId.HasValue)
            {
                return null;
            }

            return _uow.Companies.Get(application.CompanyId.Value);
        }

        private static string FormatInterviewerDisplayName(User interviewer)
        {
            if (interviewer == null)
            {
                return "Interviewer";
            }

            var fullName = string.Format("{0} {1}", interviewer.FirstName, interviewer.LastName).Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            return string.IsNullOrWhiteSpace(interviewer.UserName) ? "Interviewer" : interviewer.UserName.Trim();
        }
    }
}
