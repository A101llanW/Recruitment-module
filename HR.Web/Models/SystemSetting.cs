using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class SystemSetting
    {
        [Key]
        public string SettingKey { get; set; }

        [Required]
        public string SettingValue { get; set; }

        [StringLength(200)]
        public string Description { get; set; }

        public bool IsEncrypted { get; set; }
    }
}
