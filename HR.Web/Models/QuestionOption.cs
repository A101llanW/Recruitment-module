using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace HR.Web.Models
{
    public class QuestionOption
    {
        public int Id { get; set; }

        [ForeignKey("Question")]
        public int QuestionId { get; set; }
        public string Text { get; set; }
        public decimal Points { get; set; } // default points for this choice

        public virtual Question Question { get; set; }
        public virtual ICollection<PositionQuestionOption> PositionQuestionOptions { get; set; }
    }
}













