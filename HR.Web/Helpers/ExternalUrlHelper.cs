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

        private static Uri ResolveBaseUri(Uri requestUrl)
        {
            var configuredBaseUri = GetConfiguredBaseUri();
            var requestBaseUri = BuildRequestBaseUri(requestUrl);

            Uri resolved;
            if (configuredBaseUri != null)
            {
                if (ShouldPreferRequestUrl(configuredBaseUri, requestUrl))
                {
                    resolved = requestBaseUri ?? configuredBaseUri;
                }
                else
                {
                    resolved = configuredBaseUri;
                }
            }
            else
            {
                resolved = requestBaseUri ?? new Uri(DefaultBaseUrl, UriKind.Absolute);
            }

            return resolved ?? new Uri(DefaultBaseUrl, UriKind.Absolute);
        }

        private static Uri GetConfiguredBaseUri()
        {
            var settingsService = new SettingsService();
            var configured = settingsService.GetSetting("ExternalBaseUrl") ?? ConfigurationManager.AppSettings["ExternalBaseUrl"];
            if (string.IsNullOrWhiteSpace(configured))
            {
                return null;
            }

            var trimmed = configured.Trim().TrimEnd('/');
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var parsedUri) ? parsedUri : null;
        }

        private static Uri BuildRequestBaseUri(Uri requestUrl)
        {
            if (requestUrl == null)
            {
                return null;
            }

            var requestBaseUrl = string.Format(
                "{0}://{1}:{2}",
                requestUrl.Scheme,
                requestUrl.Host,
                requestUrl.Port);

            return Uri.TryCreate(requestBaseUrl, UriKind.Absolute, out var parsedUri) ? parsedUri : null;
        }

        private static bool ShouldPreferRequestUrl(Uri configuredUri, Uri requestUrl)
        {
            if (configuredUri == null || requestUrl == null)
            {
                return requestUrl != null;
            }

            if (!IsLoopbackHost(configuredUri.Host) || !IsLoopbackHost(requestUrl.Host))
            {
                return false;
            }

            return configuredUri.Port != requestUrl.Port ||
                   !string.Equals(configuredUri.Scheme, requestUrl.Scheme, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldPreferRequestUrl(string configuredBaseUrl, Uri requestUrl)
        {
            if (string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                return requestUrl != null;
            }

            return Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var configuredUri)
                && ShouldPreferRequestUrl(configuredUri, requestUrl);
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
