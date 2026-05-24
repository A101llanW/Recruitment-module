using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class User : ITenantEntity
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(100)]
        public string FirstName { get; set; }

        [Required, StringLength(100)]
        public string LastName { get; set; }

        [Required, StringLength(100)]
        public string UserName { get; set; }

        [Required, StringLength(100)]
        public string Email { get; set; }

        [Required, StringLength(50)]
        public string Role { get; set; } // Admin, HR

        public int? RoleDefinitionId { get; set; }
        public virtual RoleDefinition RoleDefinition { get; set; }

        [StringLength(20)]
        public string Phone { get; set; }

        [StringLength(256)]
        public string PasswordHash { get; set; }

        public User()
        {
            RequirePasswordChange = false;
        }

        public bool RequirePasswordChange { get; set; }
        
        public System.DateTime? LastPasswordChange { get; set; }
        
        public System.DateTime? PasswordChangeExpiry { get; set; }

        [StringLength(500)]
        public string AccessToken { get; set; }

        [StringLength(500)]
        public string RefreshToken { get; set; }

        public System.DateTime? TokenExpiry { get; set; }

        public virtual ICollection<Interview> Interviews { get; set; }

        [StringLength(256)]
        public string TwoFactorSecret { get; set; }

        public bool IsTwoFactorEnabled { get; set; }

        [StringLength(50)]
        public string MfaMethod { get; set; } // App, Email, SMS

        [StringLength(10)]
        public string TwoFactorCode { get; set; }

        public System.DateTime? TwoFactorExpiry { get; set; }

        public System.DateTime? PrivacyAcceptedAt { get; set; }
        public System.DateTime? TermsAcceptedAt { get; set; }

        [StringLength(20)]
        public string PrivacyVersion { get; set; }

        [StringLength(20)]
        public string TermsVersion { get; set; }
        
        // Email Verification
        public bool IsEmailVerified { get; set; }
        [StringLength(10)]
        public string EmailVerificationCode { get; set; }
        public System.DateTime? EmailVerificationExpiry { get; set; }

        /// <summary>When true, user may be assigned as an interview panelist for workflow routing.</summary>
        public bool IsPanelist { get; set; }
    }
}










































