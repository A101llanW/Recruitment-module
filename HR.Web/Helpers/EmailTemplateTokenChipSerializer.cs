using System;
using System.Collections.Generic;
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
        private struct TokenAdminMetadata
        {
            public string Label;
            public string Icon;
            public string HelpText;
        }

        private static readonly Dictionary<string, TokenAdminMetadata> AdminTokenMetadata =
            new Dictionary<string, TokenAdminMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "CandidateName",
                    new TokenAdminMetadata
                    {
                        Label = "Candidate name",
                        Icon = "fa-user",
                        HelpText = "The applicant's name."
                    }
                },
                {
                    "PositionTitle",
                    new TokenAdminMetadata
                    {
                        Label = "Job title",
                        Icon = "fa-briefcase",
                        HelpText = "The role title."
                    }
                },
                {
                    "CompanyName",
                    new TokenAdminMetadata
                    {
                        Label = "Company name",
                        Icon = "fa-link",
                        HelpText = "Your company name."
                    }
                },
                {
                    "CustomMessageBlock",
                    new TokenAdminMetadata
                    {
                        Label = "Custom message",
                        Icon = "fa-comment-alt",
                        HelpText = "Extra wording from the send screen, if provided."
                    }
                },
                {
                    "InterviewDateTime",
                    new TokenAdminMetadata
                    {
                        Label = "Interview date & time",
                        Icon = "fa-clock",
                        HelpText = "Interview date and time."
                    }
                },
                {
                    "InterviewMode",
                    new TokenAdminMetadata
                    {
                        Label = "Interview mode",
                        Icon = "fa-video",
                        HelpText = "How the interview is held."
                    }
                },
                {
                    "InterviewerName",
                    new TokenAdminMetadata
                    {
                        Label = "Interviewer name",
                        Icon = "fa-user-tie",
                        HelpText = "The interviewer's name."
                    }
                },
                {
                    "QuestionnaireStageLink",
                    new TokenAdminMetadata
                    {
                        Label = "Questionnaire stage link",
                        Icon = "fa-link",
                        HelpText = "Link to the questionnaire stage HR opened for you."
                    }
                },
                {
                    "ResetLink",
                    new TokenAdminMetadata
                    {
                        Label = "Reset link",
                        Icon = "fa-key",
                        HelpText = "Password reset link (filled in by the system)."
                    }
                },
                {
                    "SecurityCode",
                    new TokenAdminMetadata
                    {
                        Label = "Security code",
                        Icon = "fa-shield-alt",
                        HelpText = "One-time code (filled in by the system)."
                    }
                }
            };

        private static readonly TokenAdminMetadata DefaultTokenMetadata = new TokenAdminMetadata
        {
            Label = null,
            Icon = "fa-tag",
            HelpText = "Filled in automatically when the email is sent."
        };

        private static readonly TokenAdminMetadata EmptyTokenMetadata = new TokenAdminMetadata
        {
            Label = "Field",
            Icon = "fa-tag",
            HelpText = "Filled in automatically when the email is sent."
        };

        private static readonly Dictionary<string, string> AdminTokenAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "StageTwoLink", "QuestionnaireStageLink" }
            };

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
                return EmptyTokenMetadata.Label;
            }

            var metadata = ResolveTokenMetadata(tokenName);
            return metadata.Label ?? tokenName.Trim();
        }

        /// <summary>Font Awesome icon class fragment (e.g. fa-user) for admin UI.</summary>
        public static string AdminTokenIconClass(string tokenName)
        {
            if (string.IsNullOrEmpty(tokenName))
            {
                return EmptyTokenMetadata.Icon;
            }

            return ResolveTokenMetadata(tokenName).Icon;
        }

        /// <summary>Tooltip / aria description for admin insert buttons.</summary>
        public static string AdminTokenHelpText(string tokenName)
        {
            if (string.IsNullOrEmpty(tokenName))
            {
                return EmptyTokenMetadata.HelpText;
            }

            return ResolveTokenMetadata(tokenName).HelpText;
        }

        private static TokenAdminMetadata ResolveTokenMetadata(string tokenName)
        {
            var name = tokenName.Trim();
            string canonicalName;
            if (AdminTokenAliases.TryGetValue(name, out canonicalName))
            {
                name = canonicalName;
            }

            TokenAdminMetadata metadata;
            return AdminTokenMetadata.TryGetValue(name, out metadata) ? metadata : DefaultTokenMetadata;
        }
    }
}
