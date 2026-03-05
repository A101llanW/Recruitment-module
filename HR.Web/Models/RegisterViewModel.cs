using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class RegisterViewModel
    {
        [Required, StringLength(100), Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required, StringLength(100), Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required, StringLength(100)]
        public string UserName { get; set; }

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; }

        [StringLength(50)]
        public string Role { get; set; } // Admin or Client

        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required, DataType(DataType.Password), Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        public System.Collections.Generic.List<Company> Companies { get; set; }
    }
}
