using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class AuditLog : ITenantEntity
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(100)]
        public string Username { get; set; }

        [Required, StringLength(100)]
        public string Action { get; set; }

        [Required, StringLength(100)]
        public string Controller { get; set; }

        public string EntityId { get; set; }

        public string OldValues { get; set; }

        public string NewValues { get; set; }

        [Required, StringLength(45)]
        public string IPAddress { get; set; }

        public DateTime Timestamp { get; set; }

        public string UserAgent { get; set; }

        public bool WasSuccessful { get; set; }

        public string ErrorMessage { get; set; }
    }
}
