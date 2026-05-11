using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    /// <summary>
    /// Optional HR (or other internal) addresses that admins may CC on candidate-facing emails for a company.
    /// </summary>
    public class CompanyHrCcEmail
    {
        public int Id { get; set; }

        [Required]
        public int CompanyId { get; set; }

        public virtual Company Company { get; set; }

        [Required, StringLength(255), EmailAddress]
        public string Email { get; set; }

        [StringLength(150)]
        public string Label { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public CompanyHrCcEmail()
        {
            IsActive = true;
            CreatedDate = DateTime.UtcNow;
        }
    }
}
