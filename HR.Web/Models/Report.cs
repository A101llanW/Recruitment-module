using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class Report
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required, StringLength(50)]
        public string Type { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? GeneratedDate { get; set; }

        public string GeneratedBy { get; set; }

        [StringLength(500)]
        public string FilePath { get; set; }

        public bool IsActive { get; set; }

        public string Parameters { get; set; }
    }
}
