using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class QuestionAdminViewModel
    {
        public int? Id { get; set; }

        [Required, StringLength(255)]
        public string Text { get; set; }

        // Question type/category: use one of the allowed values (e.g. "Text", "Choice", "Number", "Rating")
        public string Type { get; set; }
        public bool IsActive { get; set; }

        public QuestionAdminViewModel()
        {
            Options = new List<QuestionOptionVM>();
        }

        public List<QuestionOptionVM> Options { get; set; }

        // Central list of allowed types for dropdowns / validation
        public static readonly string[] AllowedTypes = new[]
        {
            "Text",         // Free-text answer
            "Choice",       // Single/multiple choice using options
            "Number",       // Numeric input
            "Rating"        // Rating scale (e.g. 1-5)
        };
    }

    public class QuestionOptionVM
    {
        public int? Id { get; set; }
        public string Text { get; set; }
        public decimal Points { get; set; }
    }
}






