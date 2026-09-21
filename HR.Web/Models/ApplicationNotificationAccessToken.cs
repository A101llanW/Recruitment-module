using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    /// <summary>
    /// Time-limited token granting read-only access to a single application summary for a notify recipient.
    /// </summary>
    public class ApplicationNotificationAccessToken
    {
        public int Id { get; set; }

        [Required]
        public int ApplicationId { get; set; }

        public virtual Application Application { get; set; }

        [Required]
        public int RecipientId { get; set; }

        public virtual CompanyApplicationNotifyRecipient Recipient { get; set; }

        [Required, StringLength(64)]
        public string Token { get; set; }

        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? RevokedAt { get; set; }

        public ApplicationNotificationAccessToken()
        {
            CreatedDate = DateTime.UtcNow;
        }

        public bool IsValid()
        {
            return RevokedAt == null && ExpiresAt > DateTime.UtcNow;
        }
    }
}
