using System;
using System.Configuration;
using System.Web;

using HR.Web.Services;

namespace HR.Web.Helpers
{
    /// <summary>
    /// Helper for generating externally reachable base URLs.
    /// Uses Web.config appSetting "ExternalBaseUrl" when set; otherwise falls back to the current request URL.
    /// </summary>
    public static class ExternalUrlHelper
    {
        public static string GetBaseUrl(HttpRequestBase request)
        {
            var settingsService = new SettingsService();
            var externalBaseUrl = settingsService.GetSetting("ExternalBaseUrl") ?? ConfigurationManager.AppSettings["ExternalBaseUrl"];
            if (!string.IsNullOrWhiteSpace(externalBaseUrl))
            {
                return externalBaseUrl.TrimEnd('/');
            }

            if (request != null && request.Url != null)
            {
                return string.Format("{0}://{1}:{2}",
                    request.Url.Scheme,
                    request.Url.Host,
                    request.Url.Port);
            }

            return "http://localhost:8080";
        }

        public static string GetBaseUrl(HttpRequest request)
        {
            if (request == null) return "http://localhost:8080";

            var settingsService = new SettingsService();
            var externalBaseUrl = settingsService.GetSetting("ExternalBaseUrl") ?? ConfigurationManager.AppSettings["ExternalBaseUrl"];
            if (!string.IsNullOrWhiteSpace(externalBaseUrl))
            {
                return externalBaseUrl.TrimEnd('/');
            }

            if (request.Url != null)
            {
                return string.Format("{0}://{1}:{2}",
                    request.Url.Scheme,
                    request.Url.Host,
                    request.Url.Port);
            }

            return "http://localhost:8080";
        }
    }
}

