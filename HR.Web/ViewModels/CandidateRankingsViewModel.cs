using System;
using System.Collections.Generic;
using HR.Web.Models;

namespace HR.Web.ViewModels
{
    /// <summary>
    /// ViewModel for displaying candidates ranked by position
    /// Allows admins to filter and view candidate scores grouped by job position
    /// </summary>
    public class CandidateRankingsViewModel
    {
        /// <summary>
        /// Dictionary grouping candidates by Position
        /// Key: Position object, Value: List of ranked candidates for that position
        /// </summary>
        public Dictionary<Position, List<CandidateApplicationScore>> CandidatesByPosition { get; set; }

        /// <summary>
        /// List of all positions for dropdown filter
        /// </summary>
        public List<Position> Positions { get; set; }
    }

    /// <summary>
    /// Represents a candidate application with scoring information
    /// Used to display candidate rankings in the admin view
    /// </summary>
    public class CandidateApplicationScore
    {
        /// <summary>
        /// Unique application identifier
        /// </summary>
        public int ApplicationId { get; set; }

        /// <summary>
        /// Full name of the candidate
        /// </summary>
        public string CandidateName { get; set; }

        /// <summary>
        /// Email address of the candidate
        /// </summary>
        public string CandidateEmail { get; set; }

        /// <summary>
        /// Total score based on questionnaire responses and other scoring logic
        /// This is the primary ranking metric
        /// </summary>
        public decimal TotalScore { get; set; }

        /// <summary>
        /// Score from questionnaire responses (if applicable)
        /// </summary>
        public decimal QuestionnaireScore { get; set; }

        /// <summary>
        /// Maximum possible questionnaire score
        /// </summary>
        public decimal MaxQuestionnaireScore { get; set; }

        /// <summary>
        /// Date the application was submitted
        /// </summary>
        public DateTime AppliedDate { get; set; }

        /// <summary>
        /// Current status of the application
        /// Values: "Pending", "Shortlisted", "Rejected", "Interviewed", "Hired"
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Position ID this candidate applied for
        /// </summary>
        public int PositionId { get; set; }
    }
}
