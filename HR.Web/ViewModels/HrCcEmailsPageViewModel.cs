using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HR.Web.Models;

namespace HR.Web.ViewModels
{
    public class UserEmailChoiceForHrCc
    {
        public int Id { get; set; }

        public string Email { get; set; }

        public string DisplayName { get; set; }
    }

    public class HrCcEmailsPageViewModel
    {
        public int? CompanyId { get; set; }

        public string CompanyName { get; set; }

        public List<CompanyHrCcEmail> Contacts { get; set; }

        public HrCcEmailAddViewModel NewEntry { get; set; }

        public List<UserEmailChoiceForHrCc> CompanyUsersForHr { get; set; }

        public bool IsSuperAdmin { get; set; }

        public List<Company> CompanyChoices { get; set; }
    }

    public class HrCcEmailAddViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Company is required.")]
        public int CompanyId { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        [Display(Name = "Email address")]
        public string Email { get; set; }

        [StringLength(150)]
        [Display(Name = "Label (optional)")]
        public string Label { get; set; }
    }
}
