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
    }
}
