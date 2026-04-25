using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class RolePermission
    {
        public int Id { get; set; }

        public int RoleDefinitionId { get; set; }
        public virtual RoleDefinition RoleDefinition { get; set; }

        [Required, StringLength(50)]
        public string ModuleKey { get; set; }

        [Required, StringLength(20)]
        public string AccessLevel { get; set; }
    }
}
