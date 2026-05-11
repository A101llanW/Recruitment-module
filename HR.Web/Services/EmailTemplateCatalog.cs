using System;
using System.Collections.Generic;
using System.Linq;

namespace HR.Web.Services
{
    public static class EmailTemplateCatalog
    {
        public const string FailedCandidateStandard = "failed_candidate_standard";
        public const string FailedCandidateNextSteps = "failed_candidate_next_steps";
        public const string InterviewCandidateStandard = "interview_candidate_standard";
        public const string InterviewCandidateReminder = "interview_candidate_reminder";
        public const string InterviewerAssignedStandard = "interviewer_assigned_standard";
        public const string ApplicationReceivedStandard = "application_received_standard";
        public const string SecondaryStageInvitation = "secondary_stage_invitation";
        public const string PasswordResetStandard = "password_reset_standard";
        public const string MfaCodeStandard = "mfa_code_standard";
        public const string EmailVerificationStandard = "email_verification_standard";

        public sealed class TemplateDefinition
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
            public string[] AvailableTokens { get; set; }
        }

        public sealed class RenderedTemplate
        {
            public string Subject { get; set; }
            public string BodyHtml { get; set; }
        }

        private static readonly IReadOnlyList<TemplateDefinition> Definitions = new List<TemplateDefinition>
        {
            new TemplateDefinition
            {
                Key = FailedCandidateStandard,
                DisplayName = "Failed Candidate - Standard",
                Description = "Default decline/update communication for a candidate.",
                Category = "Candidate Communication",
                AvailableTokens = new[] { "CandidateName", "PositionTitle", "CompanyName", "CustomMessageBlock" }
            },
            new TemplateDefinition
            {
                Key = FailedCandidateNextSteps,
                DisplayName = "Failed Candidate - Next Steps",
                Description = "Decline update with encouragement for future opportunities.",
                Category = "Candidate Communication",
                AvailableTokens = new[] { "CandidateName", "PositionTitle", "CompanyName", "CustomMessageBlock" }
            },
            new TemplateDefinition
            {
                Key = InterviewCandidateStandard,
                DisplayName = "Interview Candidate - Standard",
                Description = "Interview schedule/update email to candidate.",
                Category = "Interview Communication",
                AvailableTokens = new[] { "CandidateName", "PositionTitle", "InterviewDateTime", "InterviewMode", "CompanyName", "CustomMessageBlock" }
            },
            new TemplateDefinition
            {
                Key = InterviewCandidateReminder,
                DisplayName = "Interview Candidate - Reminder",
                Description = "Reminder email before interview.",
                Category = "Interview Communication",
                AvailableTokens = new[] { "CandidateName", "PositionTitle", "InterviewDateTime", "InterviewMode", "CompanyName", "CustomMessageBlock" }
            },
            new TemplateDefinition
            {
                Key = InterviewerAssignedStandard,
                DisplayName = "Interviewer Assignment",
                Description = "Notification to interviewer when assigned.",
                Category = "Interview Communication",
                AvailableTokens = new[] { "InterviewerName", "CandidateName", "PositionTitle", "InterviewDateTime", "InterviewMode", "CompanyName", "CustomMessageBlock" }
            },
            new TemplateDefinition
            {
                Key = ApplicationReceivedStandard,
                DisplayName = "Application Received",
                Description = "Acknowledgement email after candidate submits application.",
                Category = "Application Communication",
                AvailableTokens = new[] { "CandidateName", "PositionTitle", "CompanyName", "CustomMessageBlock" }
            },
            new TemplateDefinition
            {
                Key = SecondaryStageInvitation,
                DisplayName = "Secondary Stage Invitation",
                Description = "Invitation to complete the next questionnaire stage.",
                Category = "Application Communication",
                AvailableTokens = new[] { "CandidateName", "PositionTitle", "CompanyName", "StageTwoLink", "CustomMessageBlock" }
            },
            new TemplateDefinition
            {
                Key = PasswordResetStandard,
                DisplayName = "Password Reset",
                Description = "Password reset workflow email.",
                Category = "Security",
                AvailableTokens = new[] { "CompanyName", "ResetLink" }
            },
            new TemplateDefinition
            {
                Key = MfaCodeStandard,
                DisplayName = "MFA Code",
                Description = "Multi-factor authentication one-time-code email.",
                Category = "Security",
                AvailableTokens = new[] { "CompanyName", "SecurityCode" }
            },
            new TemplateDefinition
            {
                Key = EmailVerificationStandard,
                DisplayName = "Email Verification",
                Description = "One-time-code email for verification.",
                Category = "Security",
                AvailableTokens = new[] { "CompanyName", "SecurityCode" }
            }
        }.AsReadOnly();

        public static IReadOnlyList<TemplateDefinition> AllDefinitions
        {
            get { return Definitions; }
        }

        public static TemplateDefinition FindDefinition(string templateKey)
        {
            return Definitions.FirstOrDefault(d => string.Equals(d.Key, NormalizeTemplateKey(templateKey), StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryGetDefaultTemplate(string templateKey, out string subjectTemplate, out string bodyTemplate)
        {
            var normalizedKey = NormalizeTemplateKey(templateKey);
            subjectTemplate = null;
            bodyTemplate = null;

            switch (normalizedKey)
            {
                case FailedCandidateNextSteps:
                    subjectTemplate = "Application update for {{PositionTitle}}";
                    bodyTemplate =
                        "<p>Dear {{CandidateName}},</p>" +
                        "<p>Thank you for your interest in <strong>{{PositionTitle}}</strong> at <strong>{{CompanyName}}</strong>.</p>" +
                        "<p>After review, we will not be proceeding with your profile for this role right now.</p>" +
                        "<p>We encourage you to apply again when another relevant opportunity opens.</p>" +
                        "{{CustomMessageBlock}}" +
                        "<p>Regards,<br/>{{CompanyName}} Recruitment Team</p>";
                    return true;

                case InterviewCandidateReminder:
                    subjectTemplate = "Interview reminder for {{PositionTitle}}";
                    bodyTemplate =
                        "<p>Dear {{CandidateName}},</p>" +
                        "<p>This is a reminder about your interview for <strong>{{PositionTitle}}</strong>.</p>" +
                        "<p><strong>Scheduled:</strong> {{InterviewDateTime}}</p>" +
                        "<p><strong>Mode:</strong> {{InterviewMode}}</p>" +
                        "{{CustomMessageBlock}}" +
                        "<p>Best regards,<br/>{{CompanyName}} Recruitment Team</p>";
                    return true;

                case InterviewerAssignedStandard:
                    subjectTemplate = "Interview assigned for {{CandidateName}}";
                    bodyTemplate =
                        "<p>Hello {{InterviewerName}},</p>" +
                        "<p>You have been assigned an interview.</p>" +
                        "<p><strong>Candidate:</strong> {{CandidateName}}</p>" +
                        "<p><strong>Position:</strong> {{PositionTitle}}</p>" +
                        "<p><strong>Scheduled:</strong> {{InterviewDateTime}}</p>" +
                        "<p><strong>Mode:</strong> {{InterviewMode}}</p>" +
                        "{{CustomMessageBlock}}" +
                        "<p>Regards,<br/>{{CompanyName}} Recruitment Team</p>";
                    return true;

                case ApplicationReceivedStandard:
                    subjectTemplate = "Application received for {{PositionTitle}}";
                    bodyTemplate =
                        "<p>Dear {{CandidateName}},</p>" +
                        "<p>We have received your application for <strong>{{PositionTitle}}</strong> at <strong>{{CompanyName}}</strong>.</p>" +
                        "<p>Our recruitment team will review your submission and contact you with updates.</p>" +
                        "{{CustomMessageBlock}}" +
                        "<p>Thank you for your interest.<br/>{{CompanyName}} Recruitment Team</p>";
                    return true;

                case SecondaryStageInvitation:
                    subjectTemplate = "Next step: complete the questionnaire for {{PositionTitle}}";
                    bodyTemplate =
                        "<p>Dear {{CandidateName}},</p>" +
                        "<p>Congratulations. You have progressed to the next step for <strong>{{PositionTitle}}</strong> at <strong>{{CompanyName}}</strong>.</p>" +
                        "<p>Please complete the next questionnaire stage using the link below:</p>" +
                        "<p><a href='{{StageTwoLink}}'>Open questionnaire stage</a></p>" +
                        "{{CustomMessageBlock}}" +
                        "<p>Regards,<br/>{{CompanyName}} Recruitment Team</p>";
                    return true;

                case PasswordResetStandard:
                    subjectTemplate = "Password reset request - {{CompanyName}}";
                    bodyTemplate =
                        "<p>Hello,</p>" +
                        "<p>We received a request to reset your password.</p>" +
                        "<p><a href='{{ResetLink}}' style='display:inline-block;padding:10px 16px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px;'>Reset Password</a></p>" +
                        "<p>If the button does not work, copy this link into your browser:</p>" +
                        "<p style='word-break:break-all;'>{{ResetLink}}</p>" +
                        "<p>This link expires in 24 hours.</p>";
                    return true;

                case MfaCodeStandard:
                    subjectTemplate = "Your verification code - {{CompanyName}}";
                    bodyTemplate =
                        "<p>Hello,</p>" +
                        "<p>Your verification code is:</p>" +
                        "<p style='font-size:30px;letter-spacing:4px;font-weight:700;'>{{SecurityCode}}</p>" +
                        "<p>This code expires in 10 minutes.</p>";
                    return true;

                case EmailVerificationStandard:
                    subjectTemplate = "Verify your email - {{CompanyName}}";
                    bodyTemplate =
                        "<p>Hello,</p>" +
                        "<p>Use the one-time code below to verify your email address:</p>" +
                        "<p style='font-size:30px;letter-spacing:4px;font-weight:700;'>{{SecurityCode}}</p>" +
                        "<p>This code expires in 15 minutes.</p>";
                    return true;

                case InterviewCandidateStandard:
                    subjectTemplate = "Interview update for {{PositionTitle}}";
                    bodyTemplate =
                        "<p>Dear {{CandidateName}},</p>" +
                        "<p>This is an update regarding your interview for <strong>{{PositionTitle}}</strong>.</p>" +
                        "<p><strong>Scheduled:</strong> {{InterviewDateTime}}</p>" +
                        "<p><strong>Mode:</strong> {{InterviewMode}}</p>" +
                        "{{CustomMessageBlock}}" +
                        "<p>Regards,<br/>{{CompanyName}} Recruitment Team</p>";
                    return true;

                case FailedCandidateStandard:
                default:
                    subjectTemplate = "Update on your application for {{PositionTitle}}";
                    bodyTemplate =
                        "<p>Dear {{CandidateName}},</p>" +
                        "<p>Thank you for applying for <strong>{{PositionTitle}}</strong> at <strong>{{CompanyName}}</strong>.</p>" +
                        "<p>After reviewing all submissions, we are unable to proceed with your application for this opening.</p>" +
                        "{{CustomMessageBlock}}" +
                        "<p>We appreciate your interest and wish you success in your job search.</p>" +
                        "<p>Regards,<br/>{{CompanyName}} Recruitment Team</p>";
                    return true;
            }
        }

        public static RenderedTemplate Render(string templateKey, IDictionary<string, string> tokens)
        {
            string subjectTemplate;
            string bodyTemplate;
            TryGetDefaultTemplate(templateKey, out subjectTemplate, out bodyTemplate);
            return RenderRawTemplates(subjectTemplate, bodyTemplate, tokens);
        }

        public static RenderedTemplate RenderRawTemplates(string subjectTemplate, string bodyTemplate, IDictionary<string, string> tokens)
        {
            return new RenderedTemplate
            {
                Subject = ReplaceTokens(subjectTemplate, tokens),
                BodyHtml = ReplaceTokens(bodyTemplate, tokens)
            };
        }

        public static string NormalizeTemplateKey(string templateKey)
        {
            return string.IsNullOrWhiteSpace(templateKey)
                ? FailedCandidateStandard
                : templateKey.Trim().ToLowerInvariant();
        }

        private static string ReplaceTokens(string template, IDictionary<string, string> tokens)
        {
            if (string.IsNullOrEmpty(template) || tokens == null || tokens.Count == 0)
            {
                return template ?? string.Empty;
            }

            var result = template;
            foreach (var pair in tokens)
            {
                var token = "{{" + pair.Key + "}}";
                result = result.Replace(token, pair.Value ?? string.Empty);
            }

            return result;
        }
    }
}
