using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Web.Models
{
    public enum ImpersonationRequestStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled,
        Expired,
        Active
    }

    public class ImpersonationRequest : ITenantEntity
    {
        public int Id { get; set; }
        
        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(100)]
        public string RequestedBy { get; set; } // SuperAdmin Username

        [Required, StringLength(100)]
        public string RequestedFrom { get; set; } // Admin Username (Target Admin)

        public DateTime RequestDate { get; set; }

        [Column("Status")]
        public int StatusValue { get; set; }

        [NotMapped]
        public ImpersonationRequestStatus Status
        {
            get { return (ImpersonationRequestStatus)StatusValue; }
            set { StatusValue = (int)value; }
        }

        public string Reason { get; set; }

        public DateTime? DecisionDate { get; set; }
        
        public string AdminNotes { get; set; }

        public DateTime? ExpiryDate { get; set; }
    }
}
