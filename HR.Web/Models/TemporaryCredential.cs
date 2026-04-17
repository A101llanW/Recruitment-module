using System;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class TemporaryCredential
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Token { get; set; } // The unguessable URL token

        [Required]
        public string EncryptedData { get; set; } // Credential JSON (username, password, etc)

        [Required]
        public DateTime ExpiryDate { get; set; }

        public bool IsUsed { get; set; }

        public DateTime CreatedDate { get; set; }

        [StringLength(50)]
        public string CredentialType { get; set; } // e.g. "CompanyAdmin"
    }
}
