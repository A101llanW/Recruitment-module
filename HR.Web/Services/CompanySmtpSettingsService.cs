using System;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Linq;
using HR.Web.Data;
using HR.Web.Helpers;
using HR.Web.Models;

namespace HR.Web.Services
{
    public interface ICompanySmtpSettingsService
    {
        CompanySmtpSettings GetForCompany(int companyId);
        SmtpConfiguration ResolveForCompany(int? companyId);
        SmtpConfiguration ResolveGlobal();
        void Save(int companyId, CompanySmtpSettingsInput input);
        bool HasConfiguredPassword(int companyId);
    }

    public sealed class CompanySmtpSettingsInput
    {
        public bool IsEnabled { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public string SmtpUser { get; set; }
        public string SmtpPassword { get; set; }
        public bool SmtpEnableSsl { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
        public bool ClearStoredPassword { get; set; }
    }

    public class CompanySmtpSettingsService : ICompanySmtpSettingsService
    {
        private readonly HrContext _context;
        private readonly ISettingsService _settingsService;

        public CompanySmtpSettingsService()
            : this(new HrContext(), new SettingsService())
        {
        }

        public CompanySmtpSettingsService(HrContext context, ISettingsService settingsService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public CompanySmtpSettings GetForCompany(int companyId)
        {
            return _context.CompanySmtpSettings.FirstOrDefault(s => s.CompanyId == companyId);
        }

        public bool HasConfiguredPassword(int companyId)
        {
            var settings = GetForCompany(companyId);
            return settings != null && !string.IsNullOrWhiteSpace(settings.SmtpPasswordEncrypted);
        }

        public SmtpConfiguration ResolveForCompany(int? companyId)
        {
            if (companyId.HasValue)
            {
                var companyConfig = TryResolveCompanySettings(companyId.Value);
                if (companyConfig != null)
                {
                    return companyConfig;
                }
            }

            return ResolveGlobal();
        }

        public SmtpConfiguration ResolveGlobal()
        {
            return new SmtpConfiguration
            {
                Host = _settingsService.GetSetting("SmtpHost") ?? ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com",
                Port = _settingsService.GetSetting<int>("SmtpPort", int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587")),
                User = _settingsService.GetSetting("SmtpUser") ?? ConfigurationManager.AppSettings["SmtpUser"] ?? string.Empty,
                Password = _settingsService.GetSetting("SmtpPassword") ?? ConfigurationManager.AppSettings["SmtpPassword"] ?? string.Empty,
                EnableSsl = _settingsService.GetSetting<bool>("SmtpEnableSsl", bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"] ?? "true")),
                FromEmail = _settingsService.GetSetting("FromEmail") ?? ConfigurationManager.AppSettings["FromEmail"] ?? "noreply@nanosoft.com",
                FromName = _settingsService.GetSetting("FromName") ?? ConfigurationManager.AppSettings["FromName"] ?? AppConfig.ProductName,
                IsCompanyScoped = false
            };
        }

        public void Save(int companyId, CompanySmtpSettingsInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var validationError = ValidateInput(input, companyId);
            if (validationError != null)
            {
                throw new ArgumentException(validationError);
            }

            var entity = _context.CompanySmtpSettings.FirstOrDefault(s => s.CompanyId == companyId);
            if (entity == null)
            {
                entity = new CompanySmtpSettings { CompanyId = companyId };
                _context.CompanySmtpSettings.Add(entity);
            }

            entity.IsEnabled = input.IsEnabled;
            entity.SmtpHost = NormalizeOptional(input.SmtpHost);
            entity.SmtpPort = input.SmtpPort > 0 ? input.SmtpPort : 587;
            entity.SmtpUser = NormalizeOptional(input.SmtpUser);
            entity.SmtpEnableSsl = true;
            entity.FromEmail = NormalizeOptional(input.FromEmail);
            entity.FromName = NormalizeOptional(input.FromName);
            entity.UpdatedDate = DateTime.UtcNow;

            if (input.ClearStoredPassword)
            {
                entity.SmtpPasswordEncrypted = null;
            }
            else if (!string.IsNullOrWhiteSpace(input.SmtpPassword))
            {
                entity.SmtpPasswordEncrypted = EncryptionHelper.Encrypt(input.SmtpPassword.Trim());
            }

            _context.SaveChanges();
        }

        private SmtpConfiguration TryResolveCompanySettings(int companyId)
        {
            var settings = GetForCompany(companyId);
            if (settings == null || !settings.IsEnabled)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.FromEmail))
            {
                return null;
            }

            var password = string.Empty;
            if (!string.IsNullOrWhiteSpace(settings.SmtpPasswordEncrypted))
            {
                password = EncryptionHelper.Decrypt(settings.SmtpPasswordEncrypted);
            }

            return new SmtpConfiguration
            {
                Host = settings.SmtpHost.Trim(),
                Port = settings.SmtpPort > 0 ? settings.SmtpPort : 587,
                User = settings.SmtpUser ?? string.Empty,
                Password = password ?? string.Empty,
                EnableSsl = true,
                FromEmail = settings.FromEmail.Trim(),
                FromName = string.IsNullOrWhiteSpace(settings.FromName) ? AppConfig.ProductName : settings.FromName.Trim(),
                IsCompanyScoped = true
            };
        }

        private string ValidateInput(CompanySmtpSettingsInput input, int companyId)
        {
            if (companyId <= 0)
            {
                return "Company is required.";
            }

            if (!input.IsEnabled)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(input.SmtpHost))
            {
                return "SMTP host is required when company email is enabled.";
            }

            if (input.SmtpHost.IndexOf('@') >= 0 || input.SmtpHost.IndexOf('.') < 0)
            {
                return "SMTP host must be a mail server name, such as smtp.gmail.com, not an email address.";
            }

            if (input.SmtpHost.Length > 255)
            {
                return "SMTP host must be 255 characters or fewer.";
            }

            if (input.SmtpPort <= 0 || input.SmtpPort > 65535)
            {
                return "SMTP port must be between 1 and 65535.";
            }

            if (!string.IsNullOrWhiteSpace(input.SmtpUser) && input.SmtpUser.Length > 255)
            {
                return "SMTP username must be 255 characters or fewer.";
            }

            if (string.IsNullOrWhiteSpace(input.FromEmail))
            {
                return "From email is required when company email is enabled.";
            }

            if (!new EmailAddressAttribute().IsValid(input.FromEmail.Trim()))
            {
                return "From email is not a valid email address.";
            }

            if (!string.IsNullOrWhiteSpace(input.FromName) && input.FromName.Length > 150)
            {
                return "From display name must be 150 characters or fewer.";
            }

            var hasExistingPassword = HasConfiguredPassword(companyId);
            if (!hasExistingPassword && string.IsNullOrWhiteSpace(input.SmtpPassword) && !input.ClearStoredPassword)
            {
                return "SMTP password is required when enabling company email for the first time.";
            }

            return null;
        }

        private static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
