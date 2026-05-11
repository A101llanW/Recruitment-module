using System.ComponentModel.DataAnnotations;

namespace HR.Web.ViewModels
{
    public class CoverLetterViewModel
    {
        public int PositionId { get; set; }
        public string PositionTitle { get; set; }

        [Required]
        [Display(Name = "Cover Letter")]
        [StringLength(8000, ErrorMessage = "Cover letter is too long (max 8000 characters).")]
        public string CoverLetter { get; set; }
    }
}

