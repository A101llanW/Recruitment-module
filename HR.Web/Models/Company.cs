using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class Company
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required, StringLength(50)]
        public string Slug { get; set; } // Human-readable identifier

        [StringLength(100)]
        public string AccessToken { get; set; } // Secure, opaque token for URLs

        public Company()
        {
            IsActive = true;
            CreatedDate = DateTime.Now;
        }

        public bool IsActive { get; set; }

        public DateTime? LicenseExpiryDate { get; set; }

        public DateTime CreatedDate { get; set; }

        [StringLength(260)]
        public string LogoPath { get; set; }

        // Navigation properties
        public virtual ICollection<User> Users { get; set; }
        public virtual ICollection<Department> Departments { get; set; }
        public virtual ICollection<Position> Positions { get; set; }
        public virtual ICollection<Applicant> Applicants { get; set; }
        public virtual ICollection<Question> Questions { get; set; }
        public virtual ICollection<RoleDefinition> RoleDefinitions { get; set; }
        public virtual ICollection<AuditLog> AuditLogs { get; set; }
        public virtual ICollection<CompanyHrCcEmail> HrCcEmails { get; set; }
    }
}
