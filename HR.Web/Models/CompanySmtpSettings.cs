using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    /// <summary>
    /// Per-company SMTP configuration for outbound candidate and tenant-scoped emails.
    /// When disabled or incomplete, the application falls back to global SMTP settings.
    /// </summary>
    public class CompanySmtpSettings
    {
        [Key]
        public int CompanyId { get; set; }

        public virtual Company Company { get; set; }

        public bool IsEnabled { get; set; }

        [StringLength(255)]
        public string SmtpHost { get; set; }

        public int SmtpPort { get; set; }

        [StringLength(255)]
        public string SmtpUser { get; set; }

        /// <summary>
        /// Encrypted at rest via EncryptionHelper.
        /// </summary>
        [StringLength(1024)]
        public string SmtpPasswordEncrypted { get; set; }

        public bool SmtpEnableSsl { get; set; }

        [StringLength(255), EmailAddress]
        public string FromEmail { get; set; }

        [StringLength(150)]
        public string FromName { get; set; }

        public DateTime UpdatedDate { get; set; }

        public CompanySmtpSettings()
        {
            SmtpPort = 587;
            SmtpEnableSsl = true;
            IsEnabled = false;
            UpdatedDate = DateTime.UtcNow;
        }
    }

}
