using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.ViewModels
{
    public class ProfileViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Phone Number")]
        [DataType(DataType.PhoneNumber)]
        public string Phone { get; set; }

        [Display(Name = "Username")]
        public string UserName { get; set; }

        public string Role { get; set; }
        public string CompanyName { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsTenantClientUser { get; set; }

        [Display(Name = "Location")]
        public string Location { get; set; }

        [Display(Name = "Total Years Experience")]
        [Range(0, 60)]
        public decimal? TotalYearsExperience { get; set; }

        [Display(Name = "Relevant Years Experience")]
        [Range(0, 60)]
        public decimal? RelevantYearsExperience { get; set; }

        [Display(Name = "Most Recent Company")]
        public string MostRecentCompany { get; set; }

        [Display(Name = "Most Recent Job Title")]
        public string MostRecentTitle { get; set; }

        [Display(Name = "Most Recent Start Date")]
        [DataType(DataType.Date)]
        public System.DateTime? MostRecentStartDate { get; set; }

        [Display(Name = "Most Recent End Date")]
        [DataType(DataType.Date)]
        public System.DateTime? MostRecentEndDate { get; set; }

        [Display(Name = "Second Most Recent Company")]
        public string SecondMostRecentCompany { get; set; }

        [Display(Name = "Second Most Recent Job Title")]
        public string SecondMostRecentTitle { get; set; }

        [Display(Name = "Second Most Recent Start Date")]
        [DataType(DataType.Date)]
        public System.DateTime? SecondMostRecentStartDate { get; set; }

        [Display(Name = "Second Most Recent End Date")]
        [DataType(DataType.Date)]
        public System.DateTime? SecondMostRecentEndDate { get; set; }

        [Display(Name = "Employment Type")]
        public string EmploymentType { get; set; }

        [Display(Name = "Core Skills")]
        public string Skills { get; set; }

        [Display(Name = "Core Competencies")]
        public string Competencies { get; set; }

        [Display(Name = "Highest Education")]
        public string EducationDegree { get; set; }

        [Display(Name = "Education Institution")]
        public string EducationInstitution { get; set; }

        [Display(Name = "Key Achievement")]
        public string KeyAchievement { get; set; }

        [Display(Name = "Certifications")]
        public string Certifications { get; set; }

        [Display(Name = "Portfolio or LinkedIn URL")]
        public Uri PortfolioUrl { get; set; }

        [Display(Name = "Work Authorization")]
        public bool WorkAuthorization { get; set; }

        [Display(Name = "Notice Period / Availability")]
        public string NoticePeriod { get; set; }
    }
}
