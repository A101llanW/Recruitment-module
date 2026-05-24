using System;
using System.Security.Cryptography;
using System.Text;

namespace HR.Web.Helpers
{
    public static class UserAgentFingerprint
    {
        public static string Compute(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return "unknown";
            }

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(userAgent));
                return Convert.ToBase64String(hash).Substring(0, 16);
            }
        }
    }
}
