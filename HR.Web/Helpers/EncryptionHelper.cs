using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Configuration;

namespace HR.Web.Helpers
{
    public static class EncryptionHelper
    {
        private const string DefaultDevFallbackKey = "HR-System-Secure-2026-Key-Default";

        // Static salt for key derivation (deployment-specific key material comes from config).
        private static readonly byte[] Salt = new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };

        private static readonly Lazy<string> EncryptionKeyLazy = new Lazy<string>(ResolveEncryptionKey);

        private static string EncryptionKey
        {
            get { return EncryptionKeyLazy.Value; }
        }

        private static string ResolveEncryptionKey()
        {
            var configuredKey = WebConfigurationManager.AppSettings["SystemEncryptionKey"];
            if (!string.IsNullOrWhiteSpace(configuredKey))
            {
                return configuredKey;
            }

#if DEBUG
            var allowInsecure = string.Equals(
                WebConfigurationManager.AppSettings["AllowInsecureDefaultEncryptionKey"],
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (allowInsecure)
            {
                return DefaultDevFallbackKey;
            }
#endif

            throw new InvalidOperationException(
                "SystemEncryptionKey is not configured. Set appSettings:SystemEncryptionKey in Web.config. " +
                "For local Debug builds only, you may set AllowInsecureDefaultEncryptionKey=true to use the documented dev fallback.");
        }

        public static string Encrypt(string clearText)
        {
            if (string.IsNullOrEmpty(clearText))
            {
                return clearText;
            }

            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                using (var pdb = new Rfc2898DeriveBytes(EncryptionKey, Salt))
                {
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(clearBytes, 0, clearBytes.Length);
                        }
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return cipherText;
            }

            try
            {
                // Remove potential spaces introduced by formatting
                cipherText = cipherText.Replace(" ", "+");
                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                using (Aes encryptor = Aes.Create())
                {
                    using (var pdb = new Rfc2898DeriveBytes(EncryptionKey, Salt))
                    {
                        encryptor.Key = pdb.GetBytes(32);
                        encryptor.IV = pdb.GetBytes(16);
                        using (var ms = new MemoryStream())
                        {
                            using (var cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                            {
                                cs.Write(cipherBytes, 0, cipherBytes.Length);
                            }
                            return Encoding.Unicode.GetString(ms.ToArray());
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Decryption failed: " + ex.Message);
                throw new InvalidOperationException(
                    "Unable to decrypt the protected value. Verify SystemEncryptionKey matches the key used when the value was encrypted.",
                    ex);
            }
        }
    }
}
