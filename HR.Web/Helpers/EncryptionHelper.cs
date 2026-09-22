using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Configuration;

namespace HR.Web.Helpers
{
    public static class EncryptionHelper
    {
        private const string LegacyDefaultKey = "HR-System-Secure-2026-Key-Default";

        // Static salt for key derivation — must remain unchanged so existing ciphertext decrypts with the same key.
        private static readonly byte[] Salt = new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };

        private static string ResolveEncryptionKey()
        {
            var configuredKey = WebConfigurationManager.AppSettings["SystemEncryptionKey"];
            if (!string.IsNullOrWhiteSpace(configuredKey))
            {
                return configuredKey.Trim();
            }

            var allowInsecure = WebConfigurationManager.AppSettings["AllowInsecureDefaultEncryptionKey"];
            var allowInsecureFallback =
                string.Equals(allowInsecure, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(allowInsecure, "1", StringComparison.OrdinalIgnoreCase);

#if DEBUG
            if (allowInsecureFallback)
            {
                Trace.WriteLine("EncryptionHelper: using legacy default key because AllowInsecureDefaultEncryptionKey is enabled (DEBUG only).");
                return LegacyDefaultKey;
            }
#endif

            throw new InvalidOperationException(
                "SystemEncryptionKey is not configured. Set appSettings:SystemEncryptionKey in Web.config for this deployment. " +
                "For local development only, set AllowInsecureDefaultEncryptionKey=true in Debug builds. " +
                "Existing ciphertext decrypts only when the same key used at encryption time is configured.");
        }

        public static string Encrypt(string clearText)
        {
            if (string.IsNullOrEmpty(clearText))
            {
                return clearText;
            }

            var encryptionKey = ResolveEncryptionKey();
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                using (var pdb = new Rfc2898DeriveBytes(encryptionKey, Salt))
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
                var encryptionKey = ResolveEncryptionKey();
                cipherText = cipherText.Replace(" ", "+");
                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                using (Aes encryptor = Aes.Create())
                {
                    using (var pdb = new Rfc2898DeriveBytes(encryptionKey, Salt))
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
                Debug.WriteLine("Decryption failed: " + ex.Message);
                return cipherText;
            }
        }
    }
}
