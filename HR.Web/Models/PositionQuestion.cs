using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public class PositionQuestion
    {
        public int Id { get; set; }

        [ForeignKey("Position")]
        public int PositionId { get; set; }

        [ForeignKey("Question")]
        public int QuestionId { get; set; }

        public int Order { get; set; }

        [Range(0, 100)]
        public decimal? Weight { get; set; }

        public PositionQuestion()
        {
            IsRequired = true;
            StageNumber = 1;
        }

        public bool IsRequired { get; set; }

        /// <summary>
        /// 1-based questionnaire stage this question belongs to (must be &lt;= position.QuestionnaireStageCount).
        /// </summary>
        public int StageNumber { get; set; }

        public virtual Position Position { get; set; }
        public virtual Question Question { get; set; }
    }
}

































