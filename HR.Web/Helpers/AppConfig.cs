using System;
using System.Web.Configuration;

namespace HR.Web.Helpers
{
    /// <summary>
    /// Application-wide settings from Web.config appSettings.
    /// </summary>
    public static class AppConfig
    {
        public const string ProductNameDefault = "NanoHireHub";
        public const string PublisherNameDefault = "Nanosoft Technologies";

        /// <summary>Branding on the one-time company admin credential download file.</summary>
        public const string CredentialsFileBrandName = "Nano Hire Hub";

        public static string ProductName
        {
            get { return GetAppSetting("ProductName", ProductNameDefault); }
        }

        public static string PublisherName
        {
            get { return GetAppSetting("PublisherName", PublisherNameDefault); }
        }

        public static bool IsProduction
        {
            get
            {
                return string.Equals(
                    GetAppSetting("AppEnvironment", "Production"),
                    "Production",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsDevelopment
        {
            get { return !IsProduction; }
        }

        /// <summary>True when AppEnvironment is Remote/Dev (local or VPN testing with production-like error handling).</summary>
        public static bool IsRemoteDevelopment
        {
            get
            {
                return string.Equals(
                    GetAppSetting("AppEnvironment", "Production"),
                    "Remote/Dev",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>When true, show full exception details instead of generic error pages.</summary>
        public static bool ShowDetailedErrors
        {
            get
            {
                if (string.Equals(
                    GetAppSetting("ShowDetailedErrors", string.Empty),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return IsRemoteDevelopment;
            }
        }

        /// <summary>
        /// When true, MFA and setup-MFA accept any non-empty code. Disable before production deploy.
        /// </summary>
        public static bool AllowDevMfaBypass
        {
            get
            {
                return string.Equals(
                    GetAppSetting("AllowDevMfaBypass", "false"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string GetAppSetting(string key, string defaultValue)
        {
            var value = WebConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }
    }
}
