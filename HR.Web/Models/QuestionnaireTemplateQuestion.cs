using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public class QuestionnaireTemplateQuestion
    {
        public int Id { get; set; }

        [ForeignKey("QuestionnaireTemplate")]
        public int TemplateId { get; set; }

        [ForeignKey("Question")]
        public int QuestionId { get; set; }

        public int Order { get; set; }

        [Range(0, 100)]
        public decimal? Weight { get; set; }

        public bool IsRequired { get; set; }

        public int StageNumber { get; set; }

        public QuestionnaireTemplateQuestion()
        {
            IsRequired = true;
            StageNumber = 1;
        }

        public virtual QuestionnaireTemplate QuestionnaireTemplate { get; set; }
        public virtual Question Question { get; set; }
    }
}
