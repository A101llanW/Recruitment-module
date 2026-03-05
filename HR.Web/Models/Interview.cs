using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public class Interview : ITenantEntity
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [ForeignKey("Application")]
        public int ApplicationId { get; set; }

        [ForeignKey("Interviewer")]
        public int InterviewerId { get; set; }

        public DateTime ScheduledAt { get; set; }

        [StringLength(50)]
        public string Mode { get; set; } // Onsite, Remote

        [StringLength(500)]
        public string Notes { get; set; }

        public virtual Application Application { get; set; }
        public virtual User Interviewer { get; set; }
    }
}










































