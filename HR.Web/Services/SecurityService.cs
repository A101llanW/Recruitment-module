using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Security.Cryptography;
using HR.Web.Data;
using HR.Web.Models;
using Google.Authenticator;
using QRCoder;

namespace HR.Web.Services
{
    public class SecurityService
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        
        // Account lockout settings
        private const int MaxFailedAttempts = 5;
        private const int LockoutDurationMinutes = 30;
        
        public bool IsAccountLocked(string username, int? companyId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            return BuildRecentFailedLoginQuery(username, companyId).Count() >= MaxFailedAttempts;
        }
        
        public DateTime? GetLockoutEndTime(string username, int? companyId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            var failedAttempts = BuildRecentFailedLoginQuery(username, companyId)
                .OrderByDescending(a => a.AttemptTime)
                .Take(MaxFailedAttempts)
                .ToList();
                
            if (failedAttempts.Count >= MaxFailedAttempts)
            {
                var oldestAttempt = failedAttempts.LastOrDefault();
                return oldestAttempt != null ? (DateTime?)oldestAttempt.AttemptTime.AddMinutes(LockoutDurationMinutes) : null;
            }
            
            return null;
        }
        
        public void RecordLoginAttempt(string username, string ipAddress, bool wasSuccessful, int? companyId = null, string failureReason = null)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            var attempt = new LoginAttempt
            {
                Username = username,
                IPAddress = ipAddress ?? string.Empty,
                AttemptTime = DateTime.Now,
                WasSuccessful = wasSuccessful,
                CompanyId = companyId,
                FailureReason = failureReason
            };
            
            _uow.LoginAttempts.Add(attempt);
            _uow.Complete();
        }
        
        public int GetRemainingAttempts(string username, int? companyId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return MaxFailedAttempts;
            }

            return Math.Max(0, MaxFailedAttempts - BuildRecentFailedLoginQuery(username, companyId).Count());
        }
        
        public void ClearFailedAttempts(string username, int? companyId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            var failedAttempts = BuildFailedLoginQuery(username, companyId).ToList();
                
            foreach (var attempt in failedAttempts)
            {
                _uow.LoginAttempts.Remove(attempt);
            }
            
            _uow.Complete();
        }

        private IQueryable<LoginAttempt> BuildRecentFailedLoginQuery(string username, int? companyId)
        {
            var lockoutThreshold = DateTime.Now.AddMinutes(-LockoutDurationMinutes);
            return BuildFailedLoginQuery(username, companyId)
                .Where(a => a.AttemptTime > lockoutThreshold);
        }

        private IQueryable<LoginAttempt> BuildFailedLoginQuery(string username, int? companyId)
        {
            var normalizedUsername = username.ToLower();
            var query = _uow.LoginAttempts.GetAll()
                .Where(a => a.Username.ToLower() == normalizedUsername && !a.WasSuccessful);

            if (companyId.HasValue)
            {
                query = query.Where(a => a.CompanyId == companyId.Value);
            }

            return query;
        }
        public string GenerateSecureToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .Replace("/", "")
                .Replace("+", "")
                .Replace("=", "")
                .Substring(0, 32);
        }

        public string GenerateMfaSecret()
        {
            var bytes = new byte[10];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            // Use hex to avoid characters that QR generators might dislike, or just alphanumeric
            var res = new char[10];
            string allowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Alphanumeric without ambiguous chars
            for (int i = 0; i < 10; i++)
            {
                res[i] = allowedChars[bytes[i] % allowedChars.Length];
            }
            return new string(res);
        }

        public string GetQrCodeBase64(string username, string secret)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(secret))
            {
                return string.Empty;
            }

            var accountName = username;
            var sharedSecret = secret;
            var authenticator = new TwoFactorAuthenticator();
            var setupInfo = authenticator.GenerateSetupCode(HR.Web.Helpers.AppConfig.ProductName, accountName, sharedSecret, false, 3);
            
            // Format: otpauth://totp/Issuer:Account?secret=Secret&issuer=Issuer
            string otpAuthUrl = string.Format("otpauth://totp/{0}:{1}?secret={2}&issuer={0}", 
                HttpUtility.UrlEncode(HR.Web.Helpers.AppConfig.ProductName), 
                HttpUtility.UrlEncode(accountName), 
                sharedSecret);

            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(otpAuthUrl, QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new QRCode(qrCodeData))
            using (var bitmap = qrCode.GetGraphic(20))
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public bool ValidateTwoFactorCode(string secret, string code)
        {
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var authenticator = new TwoFactorAuthenticator();
            return authenticator.ValidateTwoFactorPIN(secret, code);
        }

        public string GenerateTemporaryCode()
        {
            var bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            uint randomValue = BitConverter.ToUInt32(bytes, 0);
            return (100000 + (randomValue % 900000)).ToString(); // 6 digits starting from 100000
        }

        public bool ValidateTemporaryCode(User user, string code)
        {
            if (user == null || string.IsNullOrEmpty(user.TwoFactorCode) || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            if (!user.TwoFactorExpiry.HasValue || user.TwoFactorExpiry.Value < DateTime.Now)
            {
                return false;
            }
            
            return user.TwoFactorCode == code;
        }
    }
}
