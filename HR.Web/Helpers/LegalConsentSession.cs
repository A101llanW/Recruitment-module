using System;
using System.Globalization;
using System.Web;

namespace HR.Web.Helpers
{
    /// <summary>
    /// Session keys and helpers for the post-password legal consent step before issuing an auth cookie.
    /// </summary>
    public static class LegalConsentSession
    {
        public const string PendingUserIdKey = "PendingLoginLegal_UserId";
        public const string PendingStartedTicksKey = "PendingLoginLegal_StartedUtcTicks";
        public const string PendingReturnUrlKey = "PendingLoginLegal_ReturnUrl";
        public const string PendingCompanyIdKey = "PendingLoginLegal_CompanyId";
        public const string PendingUsernameKey = "PendingLoginLegal_Username";

        public static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(30);

        public static void Clear(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return;
            }

            session.Remove(PendingUserIdKey);
            session.Remove(PendingStartedTicksKey);
            session.Remove(PendingReturnUrlKey);
            session.Remove(PendingCompanyIdKey);
            session.Remove(PendingUsernameKey);
        }

        public static int? TryReadCompanyId(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return null;
            }

            var raw = session[PendingCompanyIdKey];
            if (raw == null)
            {
                return null;
            }

            if (raw is int)
            {
                return (int)raw;
            }

            int parsed;
            if (int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return null;
        }

        public static int? TryReadUserId(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return null;
            }

            var raw = session[PendingUserIdKey];
            if (raw == null)
            {
                return null;
            }

            if (raw is int)
            {
                return (int)raw;
            }

            int parsed;
            if (int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return null;
        }

        public static bool IsFresh(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return false;
            }

            var ticksRaw = session[PendingStartedTicksKey] as string;
            if (string.IsNullOrWhiteSpace(ticksRaw))
            {
                return false;
            }

            long ticks;
            if (!long.TryParse(ticksRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks))
            {
                return false;
            }

            var started = new DateTime(ticks, DateTimeKind.Utc);
            return DateTime.UtcNow - started <= PendingTtl;
        }
    }
}
