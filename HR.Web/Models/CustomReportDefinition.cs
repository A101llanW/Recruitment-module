using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class CustomReportDefinition : ITenantEntity
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required, StringLength(50)]
        public string DatasetKey { get; set; }

        [Required]
        public string ConfigJson { get; set; }

        [Required, StringLength(100)]
        public string CreatedBy { get; set; }

        public DateTime CreatedOn { get; set; }

        [StringLength(100)]
        public string UpdatedBy { get; set; }

        public DateTime? UpdatedOn { get; set; }
    }
}
