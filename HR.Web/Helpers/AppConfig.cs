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

        private static string GetAppSetting(string key, string defaultValue)
        {
            var value = WebConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }
    }
}
