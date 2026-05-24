using System;

namespace HR.Web.Models
{
    public class AdminCredentialsViewModel
    {
        public string CompanyName { get; set; }
        public Uri CompanyUrl { get; set; }
        public string AdminUsername { get; set; }
        public string AdminPassword { get; set; }
    }
}
