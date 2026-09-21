using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    /// <summary>
    /// Email addresses that receive new-application notifications for a company.
    /// </summary>
    public class CompanyApplicationNotifyRecipient
    {
        public int Id { get; set; }

        [Required]
        public int CompanyId { get; set; }

        public virtual Company Company { get; set; }

        [Required, StringLength(255), EmailAddress]
        public string Email { get; set; }

        [StringLength(150)]
        public string Label { get; set; }

        /// <summary>
        /// LoginRequired: recipient must sign in to view application details.
        /// PublicReadOnly: recipient gets a signed token URL with read-only summary.
        /// </summary>
        [Required, StringLength(30)]
        public string AccessMode { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public CompanyApplicationNotifyRecipient()
        {
            AccessMode = ApplicationNotifyAccessModes.LoginRequired;
            IsActive = true;
            CreatedDate = DateTime.UtcNow;
        }
    }

    public static class ApplicationNotifyAccessModes
    {
        public const string LoginRequired = "LoginRequired";
        public const string PublicReadOnly = "PublicReadOnly";
    }
}
