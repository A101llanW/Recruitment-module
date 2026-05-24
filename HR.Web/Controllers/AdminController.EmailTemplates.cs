using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult EmailTemplates()
        {
            if (!_rolePermissionService.CanCurrentUserManageRoleDefinitions())
            {
                return new HttpStatusCodeResult(403, "Access Denied: Only full company admins and superadmins can manage email templates.");
            }

            return View(BuildEmailTemplateManagementViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult SaveEmailTemplates(EmailTemplateManagementViewModel model)
        {
            if (!_rolePermissionService.CanCurrentUserManageRoleDefinitions())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            // Reset uses the same POST endpoint as save (avoids a separate action that some hosts fail to resolve).
            if (string.Equals(Request.Form["resetAllEmailTemplatesToDefaults"], "true", StringComparison.OrdinalIgnoreCase))
            {
                return PerformResetEmailTemplatesToDefaults();
            }

            return PersistSubmittedEmailTemplates(model);
        }

        private ActionResult PersistSubmittedEmailTemplates(EmailTemplateManagementViewModel model)
        {
            model = model ?? new EmailTemplateManagementViewModel();
            var settingsService = new SettingsService();
            var emailTemplateService = new EmailTemplateService(settingsService);
            var templateItems = model.Templates != null
                ? model.Templates
                : new List<EmailTemplateEditorItemViewModel>();

            foreach (var item in templateItems)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.TemplateKey))
                {
                    continue;
                }

                var normalizedTemplateKey = EmailTemplateCatalog.NormalizeTemplateKey(item.TemplateKey);
                var definition = EmailTemplateCatalog.FindDefinition(normalizedTemplateKey);
                var templateName = definition != null ? definition.DisplayName : normalizedTemplateKey;

                var subjectEditor = item.DefaultSubjectTemplate ?? string.Empty;
                var bodyEditor = item.DefaultBodyTemplate ?? string.Empty;
                var subjectForStorage = NormalizeMojibakeArtifacts(
                    EmailBodyHtmlSanitizer.Sanitize(EmailTemplateTokenChipSerializer.EditorSubjectHtmlToPlainStorage(subjectEditor)));
                var bodyForStorage = NormalizeMojibakeArtifacts(
                    EmailBodyHtmlSanitizer.Sanitize(EmailTemplateTokenChipSerializer.EditorHtmlToStorage(bodyEditor)));

                SaveTemplateSetting(
                    settingsService,
                    emailTemplateService.GetGlobalSubjectKey(normalizedTemplateKey),
                    subjectForStorage,
                    string.Format("Default subject template for {0}", templateName));
                SaveTemplateSetting(
                    settingsService,
                    emailTemplateService.GetGlobalBodyKey(normalizedTemplateKey),
                    bodyForStorage,
                    string.Format("Default body template for {0}", templateName));
            }

            _auditService.LogAction(
                GetAuditActorName(),
                "EMAIL_TEMPLATES_UPDATED",
                "EmailTemplates",
                "global",
                true,
                string.Format("Updated {0} template definitions", templateItems.Count));

            TempData["SuccessMessage"] = "Email template settings have been saved.";
            return RedirectToEmailTemplates();
        }

        private ActionResult RedirectToEmailTemplates()
        {
            var tenant = RouteData.Values["tenant"] as string;
            if (!string.IsNullOrEmpty(tenant))
            {
                return RedirectToAction("EmailTemplates", "Admin", new { tenant });
            }

            return RedirectToAction("EmailTemplates", "Admin");
        }

        /// <summary>
        /// GET only — POST reset is handled inside <see cref="SaveEmailTemplates"/> via hidden field.
        /// Keeps bookmarks to this URL from returning 404.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult ResetEmailTemplatesToDefaults()
        {
            if (!_rolePermissionService.CanCurrentUserManageRoleDefinitions())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return RedirectToEmailTemplates();
        }

        private ActionResult PerformResetEmailTemplatesToDefaults()
        {
            var settingsService = new SettingsService();
            var emailTemplateService = new EmailTemplateService(settingsService);
            var cleared = 0;

            foreach (var definition in EmailTemplateCatalog.AllDefinitions)
            {
                var subjectKey = emailTemplateService.GetGlobalSubjectKey(definition.Key);
                var bodyKey = emailTemplateService.GetGlobalBodyKey(definition.Key);
                settingsService.SetSetting(subjectKey, string.Empty, string.Format("Default subject template for {0}", definition.DisplayName));
                settingsService.SetSetting(bodyKey, string.Empty, string.Format("Default body template for {0}", definition.DisplayName));
                cleared++;
            }

            _auditService.LogAction(
                GetAuditActorName(),
                "EMAIL_TEMPLATES_RESET_DEFAULTS",
                "EmailTemplates",
                "global",
                true,
                string.Format("Cleared {0} custom email template overrides; built-in defaults are active.", cleared));

            TempData["SuccessMessage"] = "All email templates were reset to the built-in defaults. You can edit them again anytime.";
            return RedirectToEmailTemplates();
        }

        private EmailTemplateManagementViewModel BuildEmailTemplateManagementViewModel()
        {
            var settingsService = new SettingsService();
            var emailTemplateService = new EmailTemplateService(settingsService);

            var templateItems = EmailTemplateCatalog.AllDefinitions
                .OrderBy(t => t.Category)
                .ThenBy(t => t.DisplayName)
                .Select(definition =>
                {
                    string defaultSubject;
                    string defaultBody;
                    EmailTemplateCatalog.TryGetDefaultTemplate(definition.Key, out defaultSubject, out defaultBody);

                    var globalSubject = settingsService.GetSetting(emailTemplateService.GetGlobalSubjectKey(definition.Key));
                    var globalBody = settingsService.GetSetting(emailTemplateService.GetGlobalBodyKey(definition.Key));
                    var effectiveSubject = NormalizeMojibakeArtifacts(FirstNonEmpty(globalSubject, defaultSubject));
                    var effectiveBody = NormalizeMojibakeArtifacts(
                        NormalizeEmailTemplateBodyArtifact(definition.Key, FirstNonEmpty(globalBody, defaultBody)));

                    return new EmailTemplateEditorItemViewModel
                    {
                        TemplateKey = definition.Key,
                        DisplayName = definition.DisplayName,
                        Description = definition.Description,
                        Category = definition.Category,
                        TokenHint = string.Join(", ", definition.AvailableTokens),
                        DefaultSubjectTemplate = EmailTemplateTokenChipSerializer.StoragePlainToEditorHtml(effectiveSubject),
                        DefaultBodyTemplate = EmailTemplateTokenChipSerializer.StorageHtmlToEditorHtml(effectiveBody)
                    };
                })
                .ToList();

            return new EmailTemplateManagementViewModel
            {
                Templates = templateItems
            };
        }

        private static void SaveTemplateSetting(ISettingsService settingsService, string key, string value, string description)
        {
            settingsService.SetSetting(
                key,
                string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim(),
                description);
        }

        private static string FirstNonEmpty(string first, string second)
        {
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }

            return second ?? string.Empty;
        }

        /// <summary>
        /// Cleans a historical artifact where a lone leading "p" could appear before the first HTML tag
        /// in Secondary Stage Invitation template bodies.
        /// </summary>
        private static string NormalizeEmailTemplateBodyArtifact(string templateKey, string bodyHtml)
        {
            var normalizedTemplateKey = EmailTemplateCatalog.NormalizeTemplateKey(templateKey);
            var normalizedBody = bodyHtml ?? string.Empty;
            if (!IsSecondaryStageTemplate(normalizedTemplateKey) || string.IsNullOrWhiteSpace(normalizedBody))
            {
                return normalizedBody;
            }

            return RemoveLeadingStandalonePArtifact(normalizedBody);
        }

        private static bool IsSecondaryStageTemplate(string normalizedTemplateKey)
        {
            return string.Equals(
                normalizedTemplateKey,
                EmailTemplateCatalog.SecondaryStageInvitation,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string RemoveLeadingStandalonePArtifact(string value)
        {
            return Regex.Replace(value, @"^(\s*)[pP](\s*<)", "$1$2");
        }

        private static string NormalizeMojibakeArtifacts(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            return value
                .Replace("â€”", "-")
                .Replace("â€“", "-")
                .Replace("â€˜", "'")
                .Replace("â€™", "'")
                .Replace("’", "'")
                .Replace("â€œ", "\"")
                .Replace("â€\u009d", "\"")
                .Replace("â€", string.Empty)
                .Replace("—", "-")
                .Replace("â€¦", "...")
                .Replace("Â ", " ")
                .Replace("Â", string.Empty);
        }

    }
}
