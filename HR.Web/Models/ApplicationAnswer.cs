using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public class ApplicationAnswer
    {
        public int Id { get; set; }

        [ForeignKey("Application")]
        public int ApplicationId { get; set; }

        [ForeignKey("Question")]
        public int QuestionId { get; set; }

        public string AnswerText { get; set; }

        /// <summary>
        /// Questionnaire stage this answer was submitted under.
        /// </summary>
        public int StageNumber { get; set; }

        public virtual Application Application { get; set; }
        public virtual Question Question { get; set; }
    }
}






















