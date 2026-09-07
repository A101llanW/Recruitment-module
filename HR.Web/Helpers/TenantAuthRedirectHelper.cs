using System;
using System.Collections.Generic;
using System.Web;

namespace HR.Web.Helpers
{
    public static class TenantAuthRedirectHelper
    {
        private static readonly HashSet<string> GlobalRootControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Companies",
            "Licenses",
            "Home",
            "Error",
            "Debug"
        };

        private static readonly HashSet<string> MvcControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Account",
            "Admin",
            "Applications",
            "Applicants",
            "Captcha",
            "Companies",
            "Dashboard",
            "Departments",
            "Home",
            "Interviews",
            "Licenses",
            "Onboardings",
            "Positions",
            "Questionnaire",
            "ReportGenerator",
            "Reports",
            "Error",
            "Debug"
        };

        public static string ExtractTenantSlugFromPath(string path)
        {
            return ExtractTenantSlugFromPath(path, null);
        }

        public static string ExtractTenantSlugFromPath(string path, string applicationPath)
        {
            var routePath = StripApplicationVirtualPath(StripQuery(path), applicationPath ?? GetApplicationVirtualPath());
            var segments = SplitPathSegments(routePath);
            if (segments.Length < 2)
            {
                return null;
            }

            if (segments.Length == 2 && IsMvcController(segments[0]))
            {
                return null;
            }

            if (segments.Length >= 2 && IsMvcController(segments[1]))
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
            return IsMvcController(segment) && !IsGlobalRootController(segment);
        }

        private static bool IsMvcController(string segment)
        {
            return !string.IsNullOrWhiteSpace(segment) && MvcControllers.Contains(segment);
        }

        private static bool IsGlobalRootController(string segment)
        {
            return !string.IsNullOrWhiteSpace(segment) && GlobalRootControllers.Contains(segment);
        }

        private static string[] SplitPathSegments(string path)
        {
            return (path ?? string.Empty).Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string StripQuery(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var queryIndex = path.IndexOf('?');
            return queryIndex >= 0 ? path.Substring(0, queryIndex) : path;
        }

        private static string StripApplicationVirtualPath(string path, string applicationPath)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            if (string.IsNullOrWhiteSpace(applicationPath) || applicationPath == "/")
            {
                return path;
            }

            var normalizedAppPath = applicationPath.TrimEnd('/');
            if (path.Length < normalizedAppPath.Length)
            {
                return path;
            }

            if (path.StartsWith(normalizedAppPath, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = path.Substring(normalizedAppPath.Length);
                return string.IsNullOrEmpty(remainder) ? "/" : remainder;
            }

            return path;
        }

        private static string GetApplicationVirtualPath()
        {
            try
            {
                return HttpRuntime.AppDomainAppVirtualPath ?? "/";
            }
            catch
            {
                return "/";
            }
        }
    }
}