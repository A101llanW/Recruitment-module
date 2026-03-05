using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class PasswordReset
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(255)]
        public string Token { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        public PasswordReset()
        {
            IsUsed = false;
            CreatedDate = DateTime.UtcNow;
        }

        public bool IsUsed { get; set; }

        public DateTime CreatedDate { get; set; }

        /// <summary>The IP address that originally requested the password reset link.</summary>
        [StringLength(100)]
        public string RequestingIP { get; set; }

        /// <summary>The IP address that actually completed the password reset (may differ from RequestingIP on mobile/VPN).</summary>
        [StringLength(100)]
        public string CompletedIP { get; set; }

        public virtual User User { get; set; }
    }
}
