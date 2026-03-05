using System;
using System.Collections.Generic;

namespace HR.Web.Models
{
    /// <summary>
    /// View model used to review an application questionnaire before final submission.
    /// </summary>
    public class ApplicationReviewViewModel
    {
        public int PositionId { get; set; }
        public string PositionTitle { get; set; }

        public string ApplicantName { get; set; }
        public string ApplicantEmail { get; set; }

        public ApplicationReviewViewModel()
        {
            QuestionAnswers = new List<QuestionAnswerViewModel>();
        }

        // Dynamic question answers
        public List<QuestionAnswerViewModel> QuestionAnswers { get; set; }

        // Legacy questionnaire answers (for backward compatibility)
        public string WhyInterested { get; set; }
        public string YearsInField { get; set; }
        public string YearsInRole { get; set; }
        public string ExpectedSalary { get; set; }
        public string EducationLevel { get; set; }
        public string WorkAvailability { get; set; }
        public string WorkMode { get; set; }
        public string AvailabilityToStart { get; set; }

        // Saved resume file path
        public string ResumePath { get; set; }

        // Structured scoring fields (1-5 scale)
        public string InterestLevel { get; set; }
        public string CommunicationSkills { get; set; }
        public string ProblemSolvingSkills { get; set; }
        public string TeamworkSkills { get; set; }
    }

    /// <summary>
    /// View model for a question and its answer
    /// </summary>
    public class QuestionAnswerViewModel
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string QuestionType { get; set; }
        public string Answer { get; set; }
    }
}


















