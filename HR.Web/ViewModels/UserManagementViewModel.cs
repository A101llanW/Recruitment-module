using System;
using System.ComponentModel.DataAnnotations;
using HR.Web.Models;

namespace HR.Web.ViewModels
{
    public class UserManagementViewModel
    {
        public int Id { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required, StringLength(100)]
        public string UserName { get; set; }

        [Required, StringLength(100)]
        public string Email { get; set; }

        [Required, StringLength(50)]
        public string Role { get; set; }

        public string Phone { get; set; }

        public DateTime? LastLoginDate { get; set; }

        public string LastLoginIP { get; set; }

        public bool IsLocked { get; set; }

        public DateTime? LockoutEndTime { get; set; }

        public int FailedLoginAttempts { get; set; }

        public DateTime CreatedDate { get; set; }
        
        public string CompanyName { get; set; }

        public string Status
        {
            get
            {
                if (IsLocked)
                    return "Locked";
                return "Active";
            }
        }

        public string StatusBadgeClass
        {
            get
            {
                if (IsLocked)
                    return "badge-danger";
                return "badge-success";
            }
        }

        public string RoleBadgeClass
        {
            get
            {
                var role = Role != null ? Role.ToLower() : string.Empty;
                switch (role)
                {
                    case "admin":
                        return "badge-danger";
                    case "client":
                        return "badge-primary";
                    default:
                        return "badge-secondary";
                }
            }
        }
    }

    public class UserRoleUpdateViewModel
    {
        public int UserId { get; set; }

        [Required, StringLength(100), Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required, StringLength(100), Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required, StringLength(100)]
        public string UserName { get; set; }

        [Required, StringLength(100)]
        public string Email { get; set; }

        [Required, StringLength(50)]
        public string CurrentRole { get; set; }

        [Required, StringLength(50)]
        public string NewRole { get; set; }

        public string Reason { get; set; }

        public int? CompanyId { get; set; }
        public int? CurrentCompanyId { get; set; }
        public bool RequirePasswordChange { get; set; }
        public bool CurrentRequirePasswordChange { get; set; }

        [Display(Name = "Phone")]
        public string Phone { get; set; }

        public System.Collections.Generic.List<HR.Web.Models.Company> Companies { get; set; }
    }

    public class SuperAdminUserManagementViewModel
    {
        public System.Collections.Generic.List<UserManagementViewModel> GlobalUsers { get; set; }
        public System.Collections.Generic.List<UserManagementViewModel> Admins { get; set; }
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<UserManagementViewModel>> UsersByCompany { get; set; }
    }

    public class CreateUserViewModel
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
        [Display(Name = "Username")]
        [StringLength(100)]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; }

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        [Display(Name = "Require password change on next login")]
        public bool RequirePasswordChange { get; set; }

        [Display(Name = "Phone")]
        public string Phone { get; set; }

        public System.Collections.Generic.List<HR.Web.Models.Company> Companies { get; set; }
    }
}
