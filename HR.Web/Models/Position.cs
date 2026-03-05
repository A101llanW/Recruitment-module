using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public class Position : ITenantEntity
    {
        public int Id { get; set; }

        [Required]
        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; }

        [StringLength(2000)]
        public string Description { get; set; }

        [StringLength(3000)]
        [DataType(DataType.MultilineText)]
        public string Responsibilities { get; set; }

        [StringLength(3000)]
        [DataType(DataType.MultilineText)]
        public string Qualifications { get; set; }

        [StringLength(10)]
        public string Currency { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? SalaryMin { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? SalaryMax { get; set; }

        public DateTime PostedOn { get; set; }
        public bool IsOpen { get; set; }

        [ForeignKey("Department")]
        public int DepartmentId { get; set; }
        public virtual Department Department { get; set; }

        [StringLength(200)]
        public string Location { get; set; }

        public virtual ICollection<Application> Applications { get; set; }
        public virtual ICollection<PositionQuestion> PositionQuestions { get; set; }
    }
}











