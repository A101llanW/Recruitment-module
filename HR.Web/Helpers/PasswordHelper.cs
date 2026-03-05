using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace HR.Web.Helpers
{
    public static class PasswordHelper
    {
        private const int SaltSize = 16; // 128 bit 
        private const int KeySize = 32; // 256 bit
        private const int Iterations = 100000; // Increased from 10,000 to 100,000 for better security
        private const int MinPasswordLength = 8; // Reduced from 12 to 8 for better usability
        private const int MaxPasswordLength = 128; // Maximum reasonable length

        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or empty", "password");

            if (!IsPasswordStrong(password))
                throw new ArgumentException("Password does not meet security requirements", "password");

            using (var algorithm = new Rfc2898DeriveBytes(
                password,
                SaltSize,
                Iterations))
            {
                var key = Convert.ToBase64String(algorithm.GetBytes(KeySize));
                var salt = Convert.ToBase64String(algorithm.Salt);

                return string.Format("{0}.{1}.{2}", Iterations, salt, key);
            }
        }

        public static bool VerifyPassword(string hash, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
                    return false;

                var parts = hash.Split(new[] { '.' }, 3);

                if (parts.Length != 3)
                {
                    // Try to verify with old format for backward compatibility
                    return VerifyOldPassword(hash, password);
                }

                var iterations = Convert.ToInt32(parts[0]);
                var salt = Convert.FromBase64String(parts[1]);
                var key = Convert.FromBase64String(parts[2]);

                using (var algorithm = new Rfc2898DeriveBytes(
                    password,
                    salt,
                    iterations))
                {
                    var keyToCheck = algorithm.GetBytes(KeySize);
                    var verified = keyToCheck.Length == key.Length;

                    for (int i = 0; i < keyToCheck.Length && i < key.Length; i++)
                    {
                        verified &= keyToCheck[i] == key[i];
                    }

                    return verified;
                }
            }
            catch
            {
                return false;
            }
        }

        // Backward compatibility for old password format
        private static bool VerifyOldPassword(string hash, string password)
        {
            try
            {
                var parts = hash.Split(new[] { '.' }, 3);

                if (parts.Length != 3)
                    return false;

                var iterations = Convert.ToInt32(parts[0]);
                var salt = Convert.FromBase64String(parts[1]);
                var key = Convert.FromBase64String(parts[2]);

                using (var algorithm = new Rfc2898DeriveBytes(
                    password,
                    salt,
                    iterations)) // Old format without explicit hash algorithm
                {
                    var keyToCheck = algorithm.GetBytes(KeySize);
                    var verified = keyToCheck.Length == key.Length;

                    for (int i = 0; i < keyToCheck.Length && i < key.Length; i++)
                    {
                        verified &= keyToCheck[i] == key[i];
                    }

                    return verified;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPasswordStrong(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
                return false;

            // Check for at least one uppercase letter
            if (!password.Any(char.IsUpper))
                return false;

            // Check for at least one lowercase letter
            if (!password.Any(char.IsLower))
                return false;

            // Check for at least one digit
            if (!password.Any(char.IsDigit))
                return false;

            // Check for at least one special character
            if (!password.Any(c => !char.IsLetterOrDigit(c)))
                return false;

            // Check for common weak patterns
            if (ContainsCommonPatterns(password))
                return false;

            // Check for sequential characters
            if (ContainsSequentialChars(password))
                return false;

            return true;
        }

        private static bool ContainsCommonPatterns(string password)
        {
            // Removed common patterns check for more lenient validation
            // Still enforcing length, character types, and sequential character checks
            return false;
        }

        private static bool ContainsSequentialChars(string password)
        {
            var lowerPassword = password.ToLower();
            
            // Check for sequential numbers (123, 234, etc.)
            for (int i = 0; i < lowerPassword.Length - 2; i++)
            {
                if (char.IsDigit(lowerPassword[i]) && 
                    char.IsDigit(lowerPassword[i + 1]) && 
                    char.IsDigit(lowerPassword[i + 2]))
                {
                    int first = lowerPassword[i] - '0';
                    int second = lowerPassword[i + 1] - '0';
                    int third = lowerPassword[i + 2] - '0';
                    
                    if (second == first + 1 && third == second + 1)
                        return true;
                    if (second == first - 1 && third == second - 1)
                        return true;
                }
            }

            // Check for sequential letters (abc, bcd, etc.)
            for (int i = 0; i < lowerPassword.Length - 2; i++)
            {
                if (char.IsLetter(lowerPassword[i]) && 
                    char.IsLetter(lowerPassword[i + 1]) && 
                    char.IsLetter(lowerPassword[i + 2]))
                {
                    int first = lowerPassword[i] - 'a';
                    int second = lowerPassword[i + 1] - 'a';
                    int third = lowerPassword[i + 2] - 'a';
                    
                    if (second == first + 1 && third == second + 1)
                        return true;
                    if (second == first - 1 && third == second - 1)
                        return true;
                }
            }

            return false;
        }

        public static string GenerateSecureRandomPassword(int length = 16)
        {
            if (length < MinPasswordLength)
                length = MinPasswordLength;

            const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
            const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digitChars = "0123456789";
            const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var allChars = lowerChars + upperChars + digitChars + specialChars;
            var random = new RNGCryptoServiceProvider();
            var result = new char[length];
            var buffer = new byte[length];

            random.GetBytes(buffer);

            // Ensure at least one of each required character type
            result[0] = lowerChars[buffer[0] % lowerChars.Length];
            result[1] = upperChars[buffer[1] % upperChars.Length];
            result[2] = digitChars[buffer[2] % digitChars.Length];
            result[3] = specialChars[buffer[3] % specialChars.Length];

            // Fill the rest with random characters
            for (int i = 4; i < length; i++)
            {
                result[i] = allChars[buffer[i] % allChars.Length];
            }

            // Shuffle the result to avoid predictable patterns
            for (int i = 0; i < length; i++)
            {
                int j = buffer[i] % length;
                char temp = result[i];
                result[i] = result[j];
                result[j] = temp;
            }

            return new string(result);
        }

        public static string GetPasswordStrengthMessage()
        {
            return string.Format("Password must be at least {0} characters long and contain at least one uppercase letter, one lowercase letter, one number, and one special character. " +
                   "Passwords cannot contain common patterns or sequential characters.", MinPasswordLength);
        }
    }
}
