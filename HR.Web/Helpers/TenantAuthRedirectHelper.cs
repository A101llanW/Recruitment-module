using System;
using System.Web;

namespace HR.Web.Helpers
{
    public static class TenantAuthRedirectHelper
    {
        public static string ExtractTenantSlugFromPath(string path)
        {
            var segments = SplitPathSegments(path);
            if (segments.Length >= 2 && IsTenantAwareController(segments[1]))
            {
                return segments[0];
            }

            return null;
        }

        public static string BuildLoginPath(string tenantSlug, string returnUrl)
        {
            var path = string.IsNullOrWhiteSpace(tenantSlug)
                ? "/Account/Login"
                : "/" + tenantSlug.Trim('/') + "/Account/Login";

            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return path;
            }

            return path + "?ReturnUrl=" + HttpUtility.UrlEncode(returnUrl);
        }

        public static bool IsTenantAwareController(string segment)
        {
            return segment.Equals("Applications", StringComparison.OrdinalIgnoreCase) ||
                   segment.Equals("Positions", StringComparison.OrdinalIgnoreCase) ||
                   segment.Equals("Account", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] SplitPathSegments(string path)
        {
            return (path ?? string.Empty).Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}