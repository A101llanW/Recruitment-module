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
        public static readonly string PendingUserIdSession = "PendingLoginLegal_UserId";
        public static readonly string PendingStartedTicksSession = "PendingLoginLegal_StartedUtcTicks";
        public static readonly string PendingReturnUrlSession = "PendingLoginLegal_ReturnUrl";
        public static readonly string PendingCompanyIdSession = "PendingLoginLegal_CompanyId";
        public static readonly string PendingUsernameSession = "PendingLoginLegal_Username";

        public static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(30);

        public static void Clear(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return;
            }

            session.Remove(PendingUserIdSession);
            session.Remove(PendingStartedTicksSession);
            session.Remove(PendingReturnUrlSession);
            session.Remove(PendingCompanyIdSession);
            session.Remove(PendingUsernameSession);
        }

        public static int? TryReadCompanyId(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return null;
            }

            var raw = session[PendingCompanyIdSession];
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

            var raw = session[PendingUserIdSession];
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

            var ticksRaw = session[PendingStartedTicksSession] as string;
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
