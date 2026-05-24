using System;
using System.Configuration;
using System.Net;
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
        private const string DefaultBaseUrl = "http://localhost:5002";

        public static Uri GetBaseUri(HttpRequestBase request)
        {
            return ResolveBaseUri(request != null ? request.Url : null);
        }

        public static Uri GetBaseUri(HttpRequest request)
        {
            return ResolveBaseUri(request != null ? request.Url : null);
        }

        public static string GetBaseUrl(HttpRequestBase request)
        {
            return GetBaseUri(request).ToString().TrimEnd('/');
        }

        public static string GetBaseUrl(HttpRequest request)
        {
            return GetBaseUri(request).ToString().TrimEnd('/');
        }

        private static Uri ResolveBaseUri(Uri requestUrl)
        {
            var configuredBaseUrl = GetConfiguredBaseUrl();
            var requestBaseUrl = BuildRequestBaseUrl(requestUrl);

            string resolved;
            if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                if (ShouldPreferRequestUrl(configuredBaseUrl, requestUrl))
                {
                    resolved = requestBaseUrl ?? configuredBaseUrl;
                }
                else
                {
                    resolved = configuredBaseUrl;
                }
            }
            else
            {
                resolved = requestBaseUrl ?? DefaultBaseUrl;
            }

            return Uri.TryCreate(resolved, UriKind.Absolute, out var parsedUri)
                ? parsedUri
                : new Uri(DefaultBaseUrl, UriKind.Absolute);
        }

        private static string GetConfiguredBaseUrl()
        {
            var settingsService = new SettingsService();
            var configured = settingsService.GetSetting("ExternalBaseUrl") ?? ConfigurationManager.AppSettings["ExternalBaseUrl"];
            return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim().TrimEnd('/');
        }

        private static string BuildRequestBaseUrl(Uri requestUrl)
        {
            if (requestUrl == null)
            {
                return null;
            }

            return string.Format(
                "{0}://{1}:{2}",
                requestUrl.Scheme,
                requestUrl.Host,
                requestUrl.Port);
        }

        private static bool ShouldPreferRequestUrl(string configuredBaseUrl, Uri requestUrl)
        {
            if (requestUrl == null)
            {
                return false;
            }

            Uri configuredUri;
            if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out configuredUri))
            {
                // Invalid configured URL: use live request URL if available.
                return true;
            }

            if (!IsLoopbackHost(configuredUri.Host) || !IsLoopbackHost(requestUrl.Host))
            {
                return false;
            }

            return configuredUri.Port != requestUrl.Port ||
                   !string.Equals(configuredUri.Scheme, requestUrl.Scheme, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLoopbackHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            IPAddress address;
            return IPAddress.TryParse(host, out address) && IPAddress.IsLoopback(address);
        }
    }
}

