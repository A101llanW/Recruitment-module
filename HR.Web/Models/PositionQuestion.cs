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

        public PositionQuestion()
        {
            IsRequired = true;
        }

        public bool IsRequired { get; set; }

        public virtual Position Position { get; set; }
        public virtual Question Question { get; set; }
    }
}

































