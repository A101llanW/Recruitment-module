using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public class Application : ITenantEntity
    {
        public int Id { get; set; }

        [Required]
        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [ForeignKey("Applicant")]
        public int ApplicantId { get; set; }

        [ForeignKey("Position")]
        public int PositionId { get; set; }

        [Required, StringLength(30)]
        public string Status { get; set; } // Interviewing, Offer, Hired, Rejected

        public DateTime AppliedOn { get; set; }

        [StringLength(255)]
        public string ResumePath { get; set; }

        [StringLength(30)]
        public string WorkExperienceLevel { get; set; } // No experience, Less than 1 year, etc.

        public decimal? Score { get; set; } // AI evaluation score (0-100)
        public string ScoreReason { get; set; } // Brief explanation of the score

        public virtual Applicant Applicant { get; set; }
        public virtual Position Position { get; set; }
        public virtual ICollection<ApplicationAnswer> ApplicationAnswers { get; set; }
    }
}



























