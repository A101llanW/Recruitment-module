using System;
using System.Collections.Generic;

namespace HR.Web.Services
{
    public interface IEmailTemplateService
    {
        EmailTemplateCatalog.RenderedTemplate Render(string templateKey, IDictionary<string, string> tokens, int? companyId = null);
        string GetGlobalSubjectKey(string templateKey);
        string GetGlobalBodyKey(string templateKey);
    }

    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly ISettingsService _settingsService;

        public EmailTemplateService()
            : this(new SettingsService())
        {
        }

        public EmailTemplateService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public EmailTemplateCatalog.RenderedTemplate Render(string templateKey, IDictionary<string, string> tokens, int? companyId = null)
        {
            var normalizedKey = EmailTemplateCatalog.NormalizeTemplateKey(templateKey);

            string defaultSubjectTemplate;
            string defaultBodyTemplate;
            EmailTemplateCatalog.TryGetDefaultTemplate(normalizedKey, out defaultSubjectTemplate, out defaultBodyTemplate);

            var subjectTemplate = ResolveTemplatePart(normalizedKey, true, defaultSubjectTemplate);
            var bodyTemplate = ResolveTemplatePart(normalizedKey, false, defaultBodyTemplate);

            return EmailTemplateCatalog.RenderRawTemplates(subjectTemplate, bodyTemplate, tokens);
        }

        public string GetGlobalSubjectKey(string templateKey)
        {
            return BuildGlobalTemplateKey(templateKey, "Subject");
        }

        public string GetGlobalBodyKey(string templateKey)
        {
            return BuildGlobalTemplateKey(templateKey, "BodyHtml");
        }

        private string ResolveTemplatePart(string normalizedTemplateKey, bool isSubjectPart, string fallbackValue)
        {
            var globalKey = isSubjectPart
                ? GetGlobalSubjectKey(normalizedTemplateKey)
                : GetGlobalBodyKey(normalizedTemplateKey);
            var globalValue = _settingsService.GetSetting(globalKey);
            if (!string.IsNullOrWhiteSpace(globalValue))
            {
                return globalValue;
            }

            return fallbackValue ?? string.Empty;
        }

        private static string BuildGlobalTemplateKey(string templateKey, string fieldName)
        {
            return string.Format(
                "EmailTemplate.Global.{0}.{1}",
                EmailTemplateCatalog.NormalizeTemplateKey(templateKey),
                fieldName);
        }

    }
}
