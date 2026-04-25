using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class RoleDefinition
    {
        public RoleDefinition()
        {
            CreatedDate = DateTime.Now;
            IsActive = true;
            RolePermissions = new HashSet<RolePermission>();
            Users = new HashSet<User>();
        }

        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required, StringLength(100)]
        public string CreatedByUserName { get; set; }

        public DateTime CreatedDate { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<RolePermission> RolePermissions { get; set; }
        public virtual ICollection<User> Users { get; set; }
    }
}
