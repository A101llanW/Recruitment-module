using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HR.Web.Models;

namespace HR.Web.ViewModels
{
    public class QuestionnaireTemplateListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int QuestionCount { get; set; }
        public int StageCount { get; set; }
        public bool IsActive { get; set; }
    }

    public class QuestionnaireTemplateEditViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Template name")]
        public string Name { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Range(1, 10)]
        [Display(Name = "Number of stages")]
        public int StageCount { get; set; }

        public IList<Question> AvailableQuestions { get; set; }
        public IList<int> SelectedQuestionIds { get; set; }
        public IDictionary<int, decimal> SelectedQuestionWeights { get; set; }
        public IDictionary<int, int> SelectedQuestionStages { get; set; }

        public QuestionnaireTemplateEditViewModel()
        {
            StageCount = 1;
            AvailableQuestions = new List<Question>();
            SelectedQuestionIds = new List<int>();
            SelectedQuestionWeights = new Dictionary<int, decimal>();
            SelectedQuestionStages = new Dictionary<int, int>();
        }
    }

    public class QuestionnaireTemplateAssignmentInput
    {
        public int QuestionId { get; set; }
        public int Order { get; set; }
        public decimal? Weight { get; set; }
        public int StageNumber { get; set; }
    }
}
