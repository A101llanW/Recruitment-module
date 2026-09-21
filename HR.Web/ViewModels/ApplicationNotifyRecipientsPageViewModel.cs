using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HR.Web.Models;

namespace HR.Web.ViewModels
{
    public class ApplicationNotifyRecipientsPageViewModel
    {
        public bool IsSuperAdmin { get; set; }
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; }
        public IList<Company> CompanyChoices { get; set; }
        public IList<CompanyApplicationNotifyRecipient> Recipients { get; set; }
        public ApplicationNotifyRecipientAddViewModel NewEntry { get; set; }
        public IList<UserEmailChoiceForHrCc> CompanyUsersForPick { get; set; }
    }

    public class ApplicationNotifyRecipientAddViewModel
    {
        public int CompanyId { get; set; }

        [Required, EmailAddress, StringLength(255)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [StringLength(150)]
        [Display(Name = "Label")]
        public string Label { get; set; }

        [Required]
        [Display(Name = "Access mode")]
        public string AccessMode { get; set; }

        public ApplicationNotifyRecipientAddViewModel()
        {
            AccessMode = ApplicationNotifyAccessModes.LoginRequired;
        }
    }

    public class ApplicationNotifyRecipientEditViewModel
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }

        [Required, EmailAddress, StringLength(255)]
        public string Email { get; set; }

        [StringLength(150)]
        public string Label { get; set; }

        [Required]
        public string AccessMode { get; set; }

        public bool IsActive { get; set; }
    }
}
