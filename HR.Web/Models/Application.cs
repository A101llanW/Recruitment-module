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

        public string CoverLetter { get; set; }

        public DateTime? FailedCandidateEmailSentAt { get; set; }

        /// <summary>Workflow stage (reserved for future use; default 1).</summary>
        public int CurrentStage { get; set; }

        /// <summary>Highest questionnaire stage the candidate has fully submitted (0 = none yet).</summary>
        public int LastCompletedQuestionnaireStage { get; set; }

        /// <summary>When set, candidate may complete this questionnaire stage (HR opened the next step).</summary>
        public int? PendingQuestionnaireStage { get; set; }

        public DateTime? QuestionnaireInvitedOn { get; set; }

        public decimal? LastQuestionnaireScore { get; set; }

        public virtual Applicant Applicant { get; set; }
        public virtual Position Position { get; set; }
        public virtual ICollection<ApplicationAnswer> ApplicationAnswers { get; set; }
    }
}



























