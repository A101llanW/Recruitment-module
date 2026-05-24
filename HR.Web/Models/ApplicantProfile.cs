using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public class ApplicantProfile
    {
        public int Id { get; set; }

        [Index("IX_ApplicantProfile_Applicant", IsUnique = true)]
        public int ApplicantId { get; set; }
        public virtual Applicant Applicant { get; set; }

        public string Location { get; set; }

        [Range(0, 60)]
        public decimal? TotalYearsExperience { get; set; }

        [Range(0, 60)]
        public decimal? RelevantYearsExperience { get; set; }

        public string MostRecentCompany { get; set; }

        public string MostRecentTitle { get; set; }

        public DateTime? MostRecentStartDate { get; set; }
        public DateTime? MostRecentEndDate { get; set; }

        public string SecondMostRecentCompany { get; set; }

        public string SecondMostRecentTitle { get; set; }

        public DateTime? SecondMostRecentStartDate { get; set; }
        public DateTime? SecondMostRecentEndDate { get; set; }

        public string EmploymentType { get; set; }

        public string Skills { get; set; }

        public string Competencies { get; set; }

        public string EducationDegree { get; set; }

        public string EducationInstitution { get; set; }

        public string KeyAchievement { get; set; }

        public string Certifications { get; set; }

        [Column("PortfolioUrl")]
        [MaxLength(300)]
        public string PortfolioUrlValue { get; set; }

        [NotMapped]
        public Uri PortfolioUrl
        {
            get
            {
                return string.IsNullOrWhiteSpace(PortfolioUrlValue) || !Uri.TryCreate(PortfolioUrlValue, UriKind.Absolute, out var parsedUri)
                    ? null
                    : parsedUri;
            }
            set
            {
                PortfolioUrlValue = value != null ? value.ToString() : null;
            }
        }

        public bool WorkAuthorization { get; set; }

        public string NoticePeriod { get; set; }

        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
