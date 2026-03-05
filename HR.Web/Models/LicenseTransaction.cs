using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class LicenseTransaction : ITenantEntity
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(100)]
        public string ExecutedBy { get; set; }

        public DateTime TransactionDate { get; set; }

        public DateTime? PreviousExpiry { get; set; }

        public DateTime NewExpiry { get; set; }

        public int ExtendedByValue { get; set; }

        [StringLength(20)]
        public string ExtendedByUnit { get; set; }

        public string Notes { get; set; }
    }
}
