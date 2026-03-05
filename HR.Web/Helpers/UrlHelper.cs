using System;
using System.Configuration;
using System.Web;

using HR.Web.Services;

namespace HR.Web.Helpers
{
    public static class UrlHelper
    {
        /// <summary>
        /// Gets the base URL for external access. 
        /// Checks Web.config ExternalBaseUrl first, then falls back to current request URL.
        /// </summary>
        public static string GetBaseUrl(HttpRequestBase request)
        {
            // Check if external base URL is configured in DB first
            var settingsService = new SettingsService();
            var externalBaseUrl = settingsService.GetSetting("ExternalBaseUrl") ?? ConfigurationManager.AppSettings["ExternalBaseUrl"];
            if (!string.IsNullOrWhiteSpace(externalBaseUrl))
            {
                // Remove trailing slash if present
                return externalBaseUrl.TrimEnd('/');
            }

            // Fall back to current request URL
            if (request != null && request.Url != null)
            {
                return string.Format("{0}://{1}:{2}", 
                    request.Url.Scheme, 
                    request.Url.Host, 
                    request.Url.Port);
            }

            // Last resort
            return "http://localhost:8080";
        }

        /// <summary>
        /// Gets the base URL using HttpRequest (non-base version)
        /// </summary>
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
