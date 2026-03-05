using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class LoginAttempt : ITenantEntity
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(100)]
        public string Username { get; set; }

        [Required, StringLength(45)]
        public string IPAddress { get; set; }

        public DateTime AttemptTime { get; set; }

        public bool WasSuccessful { get; set; }

        public string FailureReason { get; set; }
    }
}
