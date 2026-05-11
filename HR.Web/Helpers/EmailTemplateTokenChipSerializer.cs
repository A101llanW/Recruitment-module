using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace HR.Web.Helpers
{
    /// <summary>
    /// Converts stored templates (with <c>{{Token}}</c>) to editor HTML with non-editable icon chips, and back on save.
    /// </summary>
    public static class EmailTemplateTokenChipSerializer
    {
        private static readonly Regex StorageTokenRegex = new Regex(@"\{\{([A-Za-z0-9_]+)\}\}", RegexOptions.Compiled);

        /// <summary>
        /// Matches a chip span produced for the template editor (non-greedy inner).
        /// </summary>
        private static readonly Regex ChipSpanRegex = new Regex(
            @"<span(?=[^>]*\bhr-email-token\b)(?=[^>]*data-hr-token=""([^""]+)"")[^>]*>.*?</span>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LegacyBracketRegex = new Regex(@"\[\[([A-Za-z0-9_]+)\]\]", RegexOptions.Compiled);

        public static string StoragePlainToEditorHtml(string storagePlain)
        {
            if (string.IsNullOrEmpty(storagePlain))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var last = 0;
            foreach (Match m in StorageTokenRegex.Matches(storagePlain))
            {
                if (m.Index > last)
                {
                    sb.Append(HttpUtility.HtmlEncode(storagePlain.Substring(last, m.Index - last)));
                }

                sb.Append(BuildChipHtml(m.Groups[1].Value));
                last = m.Index + m.Length;
            }

            if (last < storagePlain.Length)
            {
                sb.Append(HttpUtility.HtmlEncode(storagePlain.Substring(last)));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Replaces only <c>{{Token}}</c> segments in HTML; leaves tags and other content unchanged (admin-trusted templates).
        /// </summary>
        public static string StorageHtmlToEditorHtml(string storageHtml)
        {
            if (string.IsNullOrEmpty(storageHtml))
            {
                return string.Empty;
            }

            return StorageTokenRegex.Replace(storageHtml, m => BuildChipHtml(m.Groups[1].Value));
        }

        public static string EditorHtmlToStorage(string editorHtml)
        {
            if (string.IsNullOrEmpty(editorHtml))
            {
                return string.Empty;
            }

            var s = editorHtml;
            s = ChipSpanRegex.Replace(s, m => "{{" + m.Groups[1].Value + "}}");
            s = LegacyBracketRegex.Replace(s, m => "{{" + m.Groups[1].Value + "}}");
            return s;
        }

        /// <summary>
        /// Subject line is stored as plain text with <c>{{tokens}}</c>; the contenteditable field may introduce HTML wrappers.
        /// </summary>
        public static string EditorSubjectHtmlToPlainStorage(string editorHtml)
        {
            if (string.IsNullOrWhiteSpace(editorHtml))
            {
                return string.Empty;
            }

            var s = EditorHtmlToStorage(editorHtml);
            s = Regex.Replace(s, "<[^>]+>", " ");
            s = HttpUtility.HtmlDecode(s);
            s = Regex.Replace(s.Trim(), @"\s+", " ");
            return s.Trim();
        }

        public static string BuildChipHtml(string tokenName)
        {
            if (string.IsNullOrWhiteSpace(tokenName))
            {
                return string.Empty;
            }

            var name = tokenName.Trim();
            var label = HttpUtility.HtmlEncode(AdminTokenShortLabel(name));
            var icon = HttpUtility.HtmlAttributeEncode(AdminTokenIconClass(name));
            var safeName = HttpUtility.HtmlAttributeEncode(name);

            return string.Format(
                "<span class=\"hr-email-token mceNonEditable\" data-hr-token=\"{0}\" contenteditable=\"false\" style=\"display:inline-flex;align-items:center;gap:4px;vertical-align:middle;margin:0 2px;padding:2px 8px;border-radius:6px;background:#e8f0fe;border:1px solid #c6dafc;font-size:13px;color:#1a3a5c;white-space:nowrap;\">" +
                "<i class=\"fas {1}\" aria-hidden=\"true\"></i> {2}</span>",
                safeName,
                icon,
                label);
        }

        /// <summary>Short label for admin UI (Razor + chip HTML).</summary>
        public static string AdminTokenShortLabel(string tokenName)
        {
            if (string.IsNullOrEmpty(tokenName))
            {
                return "Field";
            }

            switch (tokenName.Trim())
            {
                case "CandidateName":
                    return "Candidate name";
                case "PositionTitle":
                    return "Job title";
                case "CompanyName":
                    return "Company name";
                case "CustomMessageBlock":
                    return "Custom message";
                case "InterviewDateTime":
                    return "Interview date & time";
                case "InterviewMode":
                    return "Interview mode";
                case "InterviewerName":
                    return "Interviewer name";
                case "StageTwoLink":
                    return "Questionnaire stage link";
                case "ResetLink":
                    return "Reset link";
                case "SecurityCode":
                    return "Security code";
                default:
                    return tokenName;
            }
        }

        /// <summary>Font Awesome icon class fragment (e.g. fa-user) for admin UI.</summary>
        public static string AdminTokenIconClass(string tokenName)
        {
            if (string.IsNullOrEmpty(tokenName))
            {
                return "fa-tag";
            }

            switch (tokenName.Trim())
            {
                case "CandidateName":
                    return "fa-user";
                case "PositionTitle":
                    return "fa-briefcase";
                case "CompanyName":
                    return "fa-link";
                case "CustomMessageBlock":
                    return "fa-comment-alt";
                case "InterviewDateTime":
                    return "fa-clock";
                case "InterviewMode":
                    return "fa-video";
                case "InterviewerName":
                    return "fa-user-tie";
                case "StageTwoLink":
                    return "fa-link";
                case "ResetLink":
                    return "fa-key";
                case "SecurityCode":
                    return "fa-shield-alt";
                default:
                    return "fa-tag";
            }
        }

        /// <summary>Tooltip / aria description for admin insert buttons.</summary>
        public static string AdminTokenHelpText(string tokenName)
        {
            if (string.IsNullOrEmpty(tokenName))
            {
                return "Filled in automatically when the email is sent.";
            }

            switch (tokenName.Trim())
            {
                case "CandidateName":
                    return "The applicant's name.";
                case "PositionTitle":
                    return "The role title.";
                case "CompanyName":
                    return "Your company name.";
                case "CustomMessageBlock":
                    return "Extra wording from the send screen, if provided.";
                case "InterviewDateTime":
                    return "Interview date and time.";
                case "InterviewMode":
                    return "How the interview is held.";
                case "InterviewerName":
                    return "The interviewer's name.";
                case "StageTwoLink":
                    return "Link to the next questionnaire stage.";
                case "ResetLink":
                    return "Password reset link (filled in by the system).";
                case "SecurityCode":
                    return "One-time code (filled in by the system).";
                default:
                    return "Filled in automatically when the email is sent.";
            }
        }
    }
}
