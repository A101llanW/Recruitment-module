using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.ViewModels
{
    public class ApplicantProfileViewModel
    {
        public int PositionId { get; set; }
        public int ApplicantId { get; set; }
        public bool IsTechnical { get; set; }
        public string PositionTitle { get; set; }

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Phone { get; set; }

        [Required]
        public string Location { get; set; }

        [Required, Range(0, 60)]
        [Display(Name = "Total Years Experience")]
        public decimal? TotalYearsExperience { get; set; }

        [Range(0, 60)]
        [Display(Name = "Relevant Years Experience (Technical)")]
        public decimal? RelevantYearsExperience { get; set; }

        [Required]
        [Display(Name = "Most Recent Company")]
        public string MostRecentCompany { get; set; }

        [Required]
        [Display(Name = "Most Recent Job Title")]
        public string MostRecentTitle { get; set; }

        [Required, DataType(DataType.Date)]
        [Display(Name = "Most Recent Start Date")]
        public DateTime? MostRecentStartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Most Recent End Date")]
        public DateTime? MostRecentEndDate { get; set; }

        [Display(Name = "Second Most Recent Company")]
        public string SecondMostRecentCompany { get; set; }

        [Display(Name = "Second Most Recent Job Title")]
        public string SecondMostRecentTitle { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Second Most Recent Start Date")]
        public DateTime? SecondMostRecentStartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Second Most Recent End Date")]
        public DateTime? SecondMostRecentEndDate { get; set; }

        [Required]
        [Display(Name = "Employment Type")]
        public string EmploymentType { get; set; }

        [Display(Name = "Core Technical Skills")]
        public string Skills { get; set; }

        [Display(Name = "Core Competencies")]
        public string Competencies { get; set; }

        [Required]
        [Display(Name = "Highest Education")]
        public string EducationDegree { get; set; }

        [Required]
        [Display(Name = "Education Institution")]
        public string EducationInstitution { get; set; }

        [Required]
        [Display(Name = "Key Achievement")]
        public string KeyAchievement { get; set; }

        public string Certifications { get; set; }

        [Display(Name = "Portfolio or LinkedIn URL")]
        public Uri PortfolioUrl { get; set; }

        [Display(Name = "Work Authorization")]
        public bool WorkAuthorization { get; set; }

        [Required]
        [Display(Name = "Notice Period / Availability")]
        public string NoticePeriod { get; set; }
    }
}
