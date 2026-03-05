using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public class PositionQuestionOption
    {
        public int Id { get; set; }

        [ForeignKey("PositionQuestion")]
        public int PositionQuestionId { get; set; }

        [ForeignKey("QuestionOption")]
        public int QuestionOptionId { get; set; }

        // Per-position override, else fallback to QuestionOption.Points
        public decimal? Points { get; set; }

        public virtual PositionQuestion PositionQuestion { get; set; }
        public virtual QuestionOption QuestionOption { get; set; }
    }
}













