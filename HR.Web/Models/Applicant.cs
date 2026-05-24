using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class Applicant : ITenantEntity
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(200)]
        public string FullName { get; set; }

        [Required, StringLength(100)]
        public string Email { get; set; }

        [StringLength(20)]
        public string Phone { get; set; }

        public bool IsEmailVerified { get; set; }

        public System.DateTime? PrivacyAcceptedAt { get; set; }
        public System.DateTime? TermsAcceptedAt { get; set; }

        [StringLength(20)]
        public string PrivacyVersion { get; set; }

        [StringLength(20)]
        public string TermsVersion { get; set; }

        public virtual ICollection<Application> Applications { get; set; }
    }
}










































