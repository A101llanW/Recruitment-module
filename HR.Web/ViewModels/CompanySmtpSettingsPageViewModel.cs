using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HR.Web.Models;

namespace HR.Web.ViewModels
{
    public class CompanySmtpSettingsPageViewModel
    {
        public bool IsSuperAdmin { get; set; }
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; }
        public IList<Company> CompanyChoices { get; set; }
        public CompanySmtpSettingsFormModel Form { get; set; }
        public bool HasStoredPassword { get; set; }
        public bool UsesGlobalFallback { get; set; }
    }

    public class CompanySmtpSettingsFormModel
    {
        public int CompanyId { get; set; }

        [Display(Name = "Enable company SMTP")]
        public bool IsEnabled { get; set; }

        [Display(Name = "SMTP host")]
        [StringLength(255)]
        public string SmtpHost { get; set; }

        [Display(Name = "SMTP port")]
        [Range(1, 65535)]
        public int SmtpPort { get; set; }

        [Display(Name = "SMTP username")]
        [StringLength(255)]
        public string SmtpUser { get; set; }

        [Display(Name = "SMTP password")]
        [DataType(DataType.Password)]
        public string SmtpPassword { get; set; }

        [Display(Name = "Use SSL/TLS")]
        public bool SmtpEnableSsl { get; set; }

        [Display(Name = "From email")]
        [StringLength(255)]
        [EmailAddress]
        public string FromEmail { get; set; }

        [Display(Name = "From display name")]
        [StringLength(150)]
        public string FromName { get; set; }

        public bool ClearStoredPassword { get; set; }

        public CompanySmtpSettingsFormModel()
        {
            SmtpPort = 587;
            SmtpEnableSsl = true;
        }
    }

    public class CompanySmtpTestEmailViewModel
    {
        public int CompanyId { get; set; }

        [Required, EmailAddress, Display(Name = "Test recipient")]
        public string TestRecipient { get; set; }
    }
}
