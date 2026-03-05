using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public class Onboarding
    {
        public int Id { get; set; }

        [ForeignKey("Application")]
        public int ApplicationId { get; set; }

        public DateTime StartDate { get; set; }

        [StringLength(200)]
        public string Tasks { get; set; }

        [StringLength(50)]
        public string Status { get; set; } // Pending, InProgress, Completed

        public virtual Application Application { get; set; }
    }
}










































