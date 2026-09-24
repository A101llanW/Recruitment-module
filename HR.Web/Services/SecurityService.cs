using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Security.Cryptography;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Helpers;
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

        public bool IsAccountLockedForUser(User user)
        {
            if (user == null)
            {
                return false;
            }

            foreach (var identity in GetLoginIdentities(user))
            {
                if (IsAccountLocked(identity, user.CompanyId) || IsAccountLocked(identity, null))
                {
                    return true;
                }
            }

            return false;
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

        public DateTime? GetLockoutEndTimeForUser(User user)
        {
            if (user == null)
            {
                return null;
            }

            DateTime? latestEnd = null;
            foreach (var identity in GetLoginIdentities(user))
            {
                latestEnd = MaxNullableDateTime(latestEnd, GetLockoutEndTime(identity, user.CompanyId));
                latestEnd = MaxNullableDateTime(latestEnd, GetLockoutEndTime(identity, null));
            }

            return latestEnd;
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

            PersistLoginAttempt(attempt);
        }

        public void RecordVisitorActivity(int companyId, string ipAddress, string summary)
        {
            if (companyId <= 0 || string.IsNullOrWhiteSpace(summary))
            {
                return;
            }

            var attempt = new LoginAttempt
            {
                Username = "Visitor",
                IPAddress = ipAddress ?? "Unknown",
                AttemptTime = DateTime.Now,
                WasSuccessful = true,
                CompanyId = companyId,
                FailureReason = summary.Trim()
            };

            PersistLoginAttempt(attempt);
        }

        private static void PersistLoginAttempt(LoginAttempt attempt)
        {
            try
            {
                using (var uow = new UnitOfWork())
                {
                    uow.LoginAttempts.Add(attempt);
                    uow.Complete();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SecurityService] PersistLoginAttempt failed: " + ex.Message);
            }
        }
        
        public int GetRemainingAttempts(string username, int? companyId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return MaxFailedAttempts;
            }

            return Math.Max(0, MaxFailedAttempts - BuildRecentFailedLoginQuery(username, companyId).Count());
        }

        public int GetFailedAttemptCountForUser(User user)
        {
            if (user == null)
            {
                return 0;
            }

            var highestCount = 0;
            foreach (var identity in GetLoginIdentities(user))
            {
                highestCount = Math.Max(highestCount, BuildRecentFailedLoginQuery(identity, user.CompanyId).Count());
                highestCount = Math.Max(highestCount, BuildRecentFailedLoginQuery(identity, null).Count());
            }

            return highestCount;
        }

        /// <summary>
        /// Resolves lockout status for a user list with one recent-attempt read.
        /// </summary>
        public Dictionary<int, UserLockoutState> GetLockoutStates(IEnumerable<User> users)
        {
            var userList = users?.Where(user => user != null).ToList() ?? new List<User>();
            var states = new Dictionary<int, UserLockoutState>();
            if (userList.Count == 0)
            {
                return states;
            }

            var threshold = DateTime.Now.AddMinutes(-LockoutDurationMinutes);
            var recentFailures = _uow.LoginAttempts.GetAll()
                .Where(a => !a.WasSuccessful && a.AttemptTime > threshold)
                .Select(a => new
                {
                    a.Username,
                    a.CompanyId,
                    a.AttemptTime
                })
                .ToList()
                .Select(a => new RecentFailedAttempt
                {
                    Username = a.Username,
                    CompanyId = a.CompanyId,
                    AttemptTime = a.AttemptTime
                })
                .ToList();

            foreach (var user in userList)
            {
                states[user.Id] = BuildLockoutState(user, recentFailures);
            }

            return states;
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

        public void ClearFailedAttemptsForUser(User user)
        {
            if (user == null)
            {
                return;
            }

            foreach (var identity in GetLoginIdentities(user))
            {
                ClearFailedAttempts(identity, null);
            }
        }

        private static IEnumerable<string> GetLoginIdentities(User user)
        {
            if (!string.IsNullOrWhiteSpace(user.UserName))
            {
                yield return user.UserName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                var email = user.Email.Trim();
                if (string.IsNullOrWhiteSpace(user.UserName) ||
                    !string.Equals(email, user.UserName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    yield return email;
                }
            }
        }

        private static DateTime? MaxNullableDateTime(DateTime? current, DateTime? candidate)
        {
            if (!candidate.HasValue)
            {
                return current;
            }

            if (!current.HasValue || candidate.Value > current.Value)
            {
                return candidate;
            }

            return current;
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
            using (var rng = new RNGCryptoServiceProvider())
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
            using (var rng = new RNGCryptoServiceProvider())
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
            string otpAuthUrl = TotpHelper.GenerateSetupUri(HR.Web.Helpers.AppConfig.ProductName, accountName, sharedSecret);

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

            return TotpHelper.ValidatePin(secret, code, 2);
        }

        public string GenerateTemporaryCode()
        {
            var bytes = new byte[4];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            uint randomValue = BitConverter.ToUInt32(bytes, 0);
            return (100000 + (randomValue % 900000)).ToString(); // 6 digits starting from 100000
        }

        public bool ValidateTemporaryCode(User user, string code)
        {
            if (user == null || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var normalizedCode = code.Trim();
            if (string.IsNullOrEmpty(normalizedCode))
            {
                return false;
            }

            if (AppConfig.AllowDevMfaBypass)
            {
                return true;
            }

            if (string.IsNullOrEmpty(user.TwoFactorCode))
            {
                return false;
            }

            if (!user.TwoFactorExpiry.HasValue || user.TwoFactorExpiry.Value < DateTime.Now)
            {
                return false;
            }

            return string.Equals(user.TwoFactorCode.Trim(), normalizedCode, StringComparison.Ordinal);
        }

        /// <summary>
        /// Validates MFA against a fresh database read so stale EF tracked entities cannot reject a valid code.
        /// </summary>
        public bool ValidateTemporaryCodeForUserId(int userId, string code)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var normalizedCode = code.Trim();
            if (string.IsNullOrEmpty(normalizedCode))
            {
                return false;
            }

            if (AppConfig.AllowDevMfaBypass)
            {
                return true;
            }

            using (var freshUow = new UnitOfWork())
            {
                var snapshot = freshUow.Context.Database.SqlQuery<MfaCodeSnapshot>(
                        "SELECT TwoFactorCode, TwoFactorExpiry FROM dbo.Users WHERE Id = @p0",
                        userId)
                    .FirstOrDefault();

                if (snapshot == null || string.IsNullOrEmpty(snapshot.TwoFactorCode))
                {
                    return false;
                }

                if (!snapshot.TwoFactorExpiry.HasValue || snapshot.TwoFactorExpiry.Value < DateTime.Now)
                {
                    return false;
                }

                return string.Equals(snapshot.TwoFactorCode.Trim(), normalizedCode, StringComparison.Ordinal);
            }
        }

        private UserLockoutState BuildLockoutState(User user, IList<RecentFailedAttempt> recentFailures)
        {
            var state = new UserLockoutState();
            foreach (var identity in GetLoginIdentities(user))
            {
                ApplyLockoutScope(state, recentFailures, identity, user.CompanyId);
                ApplyLockoutScope(state, recentFailures, identity, null);
            }

            return state;
        }

        private static void ApplyLockoutScope(
            UserLockoutState state,
            IList<RecentFailedAttempt> recentFailures,
            string identity,
            int? companyId)
        {
            var matching = recentFailures.Where(attempt =>
                string.Equals(attempt.Username, identity, StringComparison.OrdinalIgnoreCase) &&
                (!companyId.HasValue || attempt.CompanyId == companyId.Value)).ToList();

            state.FailedLoginAttempts = Math.Max(state.FailedLoginAttempts, matching.Count);
            if (matching.Count >= MaxFailedAttempts)
            {
                state.IsLocked = true;
            }

            var newestAttempts = matching
                .OrderByDescending(attempt => attempt.AttemptTime)
                .Take(MaxFailedAttempts)
                .ToList();
            if (newestAttempts.Count < MaxFailedAttempts)
            {
                return;
            }

            var lockoutEnd = newestAttempts[newestAttempts.Count - 1].AttemptTime.AddMinutes(LockoutDurationMinutes);
            state.LockoutEndTime = MaxNullableDateTime(state.LockoutEndTime, lockoutEnd);
        }

        private sealed class RecentFailedAttempt
        {
            public string Username { get; set; }
            public int? CompanyId { get; set; }
            public DateTime AttemptTime { get; set; }
        }

        private sealed class MfaCodeSnapshot
        {
            public string TwoFactorCode { get; set; }
            public DateTime? TwoFactorExpiry { get; set; }
        }
    }

    public sealed class UserLockoutState
    {
        public bool IsLocked { get; set; }
        public DateTime? LockoutEndTime { get; set; }
        public int FailedLoginAttempts { get; set; }
    }
}
