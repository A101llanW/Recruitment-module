using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using HR.Web.Data;
using HR.Web.Helpers;
using HR.Web.Models;

namespace HR.Web.Services
{
    public interface IApplicationNotificationService
    {
        void QueueNewApplicationNotifications(int applicationId, HttpRequestBase request);
        Task SendNewApplicationNotificationsAsync(int applicationId, HttpRequestBase request);
        ApplicationNotificationSummaryModel BuildSummaryForToken(string token);
        string CreateOrGetPublicAccessToken(int applicationId, int recipientId);
    }

    public sealed class ApplicationNotificationSummaryModel
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public string CandidateName { get; set; }
        public string PositionTitle { get; set; }
        public string CompanyName { get; set; }
        public decimal? Score { get; set; }
        public bool HasScore { get; set; }
        public int Rank { get; set; }
        public int TotalApplicants { get; set; }
        public DateTime AppliedOn { get; set; }
        public string Status { get; set; }
        public string RankDescription { get; set; }
    }

    public class ApplicationNotificationService : IApplicationNotificationService
    {
        private const int TokenValidityDays = 30;

        private readonly HrContext _context;
        private readonly IEmailService _emailService;
        private readonly SecurityService _securityService;

        public ApplicationNotificationService()
            : this(new HrContext(), new EmailService(), new SecurityService())
        {
        }

        public ApplicationNotificationService(HrContext context, IEmailService emailService, SecurityService securityService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        }

        public void QueueNewApplicationNotifications(int applicationId, HttpRequestBase request)
        {
            if (applicationId <= 0)
            {
                return;
            }

            var portalBaseUrl = ExternalUrlHelper.GetBaseUri(request).ToString().TrimEnd('/');
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (var context = new HrContext())
                    {
                        var smtpSettings = new CompanySmtpSettingsService(context, new SettingsService());
                        var emailService = new EmailService(smtpSettings);
                        var service = new ApplicationNotificationService(context, emailService, new SecurityService());
                        service.SendNewApplicationNotificationsAsync(applicationId, portalBaseUrl).GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("[ApplicationNotification] Failed for application " + applicationId + ": " + ex.Message);
                }
            });
        }

        public Task SendNewApplicationNotificationsAsync(int applicationId, HttpRequestBase request)
        {
            var portalBaseUrl = ExternalUrlHelper.GetBaseUri(request).ToString().TrimEnd('/');
            return SendNewApplicationNotificationsAsync(applicationId, portalBaseUrl);
        }

        private async Task SendNewApplicationNotificationsAsync(int applicationId, string portalBaseUrl)
        {
            var application = _context.Applications
                .Include(a => a.Applicant)
                .Include(a => a.Position)
                .Include(a => a.Company)
                .FirstOrDefault(a => a.Id == applicationId);

            var companyId = application != null ? application.CompanyId : null;
            if (!companyId.HasValue && application != null && application.Position != null)
            {
                companyId = application.Position.CompanyId;
            }

            if (application == null || !companyId.HasValue)
            {
                return;
            }

            var recipients = _context.CompanyApplicationNotifyRecipients
                .Where(r => r.CompanyId == companyId.Value && r.IsActive)
                .OrderBy(r => r.SortOrder)
                .ThenBy(r => r.Email)
                .ToList();

            if (!recipients.Any())
            {
                return;
            }

            var positionApplications = _context.Applications
                .Where(a => a.PositionId == application.PositionId)
                .ToList();

            var rankResult = ApplicationRankHelper.ComputeRank(positionApplications, application.Id);
            var candidateName = application.Applicant != null && !string.IsNullOrWhiteSpace(application.Applicant.FullName)
                ? application.Applicant.FullName.Trim()
                : "Candidate";
            var positionTitle = application.Position != null && !string.IsNullOrWhiteSpace(application.Position.Title)
                ? application.Position.Title.Trim()
                : "Position";
            var companyName = application.Company != null && !string.IsNullOrWhiteSpace(application.Company.Name)
                ? application.Company.Name.Trim()
                : AppConfig.ProductName;
            var tenantSlug = application.Company != null ? application.Company.Slug : null;

            foreach (var recipient in recipients)
            {
                if (string.IsNullOrWhiteSpace(recipient.Email))
                {
                    continue;
                }

                var detailsUrl = BuildDetailsUrl(recipient, application, tenantSlug, portalBaseUrl);
                var subject = string.Format("New application: {0} — {1}", candidateName, positionTitle);
                var body = BuildNotificationEmailBody(
                    candidateName,
                    positionTitle,
                    companyName,
                    rankResult,
                    detailsUrl,
                    recipient.AccessMode);

                await _emailService.SendAsync(recipient.Email.Trim(), subject, body, companyId).ConfigureAwait(false);
            }
        }

        public string CreateOrGetPublicAccessToken(int applicationId, int recipientId)
        {
            var existing = _context.ApplicationNotificationAccessTokens
                .FirstOrDefault(t =>
                    t.ApplicationId == applicationId &&
                    t.RecipientId == recipientId &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > DateTime.UtcNow);

            if (existing != null)
            {
                return existing.Token;
            }

            var token = new ApplicationNotificationAccessToken
            {
                ApplicationId = applicationId,
                RecipientId = recipientId,
                Token = _securityService.GenerateSecureToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(TokenValidityDays),
                CreatedDate = DateTime.UtcNow
            };

            _context.ApplicationNotificationAccessTokens.Add(token);
            _context.SaveChanges();
            return token.Token;
        }

        public ApplicationNotificationSummaryModel BuildSummaryForToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return InvalidSummary("This link is invalid.");
            }

            var accessToken = _context.ApplicationNotificationAccessTokens
                .Include(t => t.Application)
                .Include(t => t.Application.Applicant)
                .Include(t => t.Application.Position)
                .Include(t => t.Application.Company)
                .Include(t => t.Recipient)
                .FirstOrDefault(t => t.Token == token.Trim());

            if (accessToken == null)
            {
                return InvalidSummary("This link is invalid.");
            }

            if (accessToken.RevokedAt.HasValue)
            {
                return InvalidSummary("This link has been revoked.");
            }

            if (accessToken.ExpiresAt <= DateTime.UtcNow)
            {
                return InvalidSummary("This link has expired.");
            }

            if (accessToken.Recipient == null || !accessToken.Recipient.IsActive)
            {
                return InvalidSummary("This link is no longer available.");
            }

            var application = accessToken.Application;
            if (application == null)
            {
                return InvalidSummary("The application is no longer available.");
            }

            var positionApplications = _context.Applications
                .Where(a => a.PositionId == application.PositionId)
                .ToList();

            var rankResult = ApplicationRankHelper.ComputeRank(positionApplications, application.Id);

            return new ApplicationNotificationSummaryModel
            {
                IsValid = true,
                CandidateName = application.Applicant != null ? application.Applicant.FullName : "Candidate",
                PositionTitle = application.Position != null ? application.Position.Title : "Position",
                CompanyName = application.Company != null ? application.Company.Name : AppConfig.ProductName,
                Score = rankResult.Score,
                HasScore = rankResult.HasScore,
                Rank = rankResult.Rank,
                TotalApplicants = rankResult.TotalApplicants,
                AppliedOn = application.AppliedOn,
                Status = application.Status,
                RankDescription = BuildRankDescription(rankResult)
            };
        }

        public static void RevokeTokensForRecipient(HrContext context, int recipientId)
        {
            var tokens = context.ApplicationNotificationAccessTokens
                .Where(t => t.RecipientId == recipientId && t.RevokedAt == null)
                .ToList();

            var revokedAt = DateTime.UtcNow;
            foreach (var token in tokens)
            {
                token.RevokedAt = revokedAt;
            }

            if (tokens.Any())
            {
                context.SaveChanges();
            }
        }

        private string BuildDetailsUrl(
            CompanyApplicationNotifyRecipient recipient,
            Application application,
            string tenantSlug,
            string portalBaseUrl)
        {
            var baseUrl = string.IsNullOrWhiteSpace(portalBaseUrl) ? string.Empty : portalBaseUrl.TrimEnd('/');
            if (string.Equals(recipient.AccessMode, ApplicationNotifyAccessModes.PublicReadOnly, StringComparison.OrdinalIgnoreCase))
            {
                var token = CreateOrGetPublicAccessToken(application.Id, recipient.Id);
                return string.Format("{0}/Applications/NotificationSummary?token={1}", baseUrl, Uri.EscapeDataString(token));
            }

            var tenantBase = string.IsNullOrWhiteSpace(tenantSlug)
                ? baseUrl
                : baseUrl + "/" + tenantSlug.Trim().TrimStart('/');
            return string.Format("{0}/Applications/Details/{1}", tenantBase.TrimEnd('/'), application.Id);
        }

        private static string BuildRankDescription(ApplicationRankResult rankResult)
        {
            if (rankResult.TotalApplicants <= 0)
            {
                return "No other applicants for this position yet.";
            }

            if (!rankResult.HasScore)
            {
                return string.Format(
                    "Rank pending — scored after questionnaire review ({0} applicant{1} total).",
                    rankResult.TotalApplicants,
                    rankResult.TotalApplicants == 1 ? string.Empty : "s");
            }

            return string.Format(
                "Rank {0} of {1} applicants for this position (higher score ranks better; tied scores share the same rank).",
                rankResult.Rank,
                rankResult.TotalApplicants);
        }

        private static string BuildNotificationEmailBody(
            string candidateName,
            string positionTitle,
            string companyName,
            ApplicationRankResult rankResult,
            string detailsUrl,
            string accessMode)
        {
            var scoreLine = rankResult.HasScore
                ? string.Format("<tr><td style=\"padding:8px;border:1px solid #ddd;\"><strong>Score</strong></td><td style=\"padding:8px;border:1px solid #ddd;\">{0:0.##} points</td></tr>", rankResult.Score.Value)
                : "<tr><td style=\"padding:8px;border:1px solid #ddd;\"><strong>Score</strong></td><td style=\"padding:8px;border:1px solid #ddd;\">Pending (questionnaire scoring)</td></tr>";

            var rankLine = string.Format(
                "<tr><td style=\"padding:8px;border:1px solid #ddd;\"><strong>Rank</strong></td><td style=\"padding:8px;border:1px solid #ddd;\">{0}</td></tr>",
                HttpUtility.HtmlEncode(BuildRankDescription(rankResult)));

            var accessNote = string.Equals(accessMode, ApplicationNotifyAccessModes.PublicReadOnly, StringComparison.OrdinalIgnoreCase)
                ? "<p style=\"color:#666;font-size:13px;\">This link provides read-only access and expires in 30 days. No login is required.</p>"
                : "<p style=\"color:#666;font-size:13px;\">Sign in with your company account to view full application details.</p>";

            return string.Format(@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""/></head>
<body style=""font-family:Arial,sans-serif;line-height:1.6;color:#333;"">
  <div style=""max-width:600px;margin:0 auto;padding:20px;"">
    <h2 style=""color:#2c3e50;"">New application received</h2>
    <p>A candidate has submitted a new application for <strong>{0}</strong>.</p>
    <table style=""border-collapse:collapse;width:100%;margin:16px 0;"">
      <tr><td style=""padding:8px;border:1px solid #ddd;""><strong>Candidate</strong></td><td style=""padding:8px;border:1px solid #ddd;"">{1}</td></tr>
      <tr><td style=""padding:8px;border:1px solid #ddd;""><strong>Position</strong></td><td style=""padding:8px;border:1px solid #ddd;"">{2}</td></tr>
      {3}
      {4}
    </table>
    <p style=""text-align:center;margin:24px 0;"">
      <a href=""{5}"" style=""display:inline-block;padding:12px 24px;background:#3498db;color:#fff;text-decoration:none;border-radius:4px;"">View application details</a>
    </p>
    {6}
    <p style=""font-size:12px;color:#999;"">Sent by {7}.</p>
  </div>
</body>
</html>",
                HttpUtility.HtmlEncode(companyName),
                HttpUtility.HtmlEncode(candidateName),
                HttpUtility.HtmlEncode(positionTitle),
                scoreLine,
                rankLine,
                HttpUtility.HtmlEncode(detailsUrl ?? string.Empty),
                accessNote,
                HttpUtility.HtmlEncode(AppConfig.ProductName));
        }

        private static ApplicationNotificationSummaryModel InvalidSummary(string message)
        {
            return new ApplicationNotificationSummaryModel
            {
                IsValid = false,
                ErrorMessage = message
            };
        }
    }
}
