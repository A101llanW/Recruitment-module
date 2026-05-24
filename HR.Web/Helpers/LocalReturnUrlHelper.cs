using System;
using System.Web;
using System.Web.Mvc;

namespace HR.Web.Helpers
{
    /// <summary>
    /// Parses and formats local return URLs for safe redirects after login/registration.
    /// </summary>
    public static class LocalReturnUrlHelper
    {
        public static bool TryParseLocalReturnUri(string returnPath, System.Web.Mvc.UrlHelper urlHelper, out Uri parsedUri)
        {
            parsedUri = null;
            if (string.IsNullOrWhiteSpace(returnPath) || urlHelper == null || !urlHelper.IsLocalUrl(returnPath))
            {
                return false;
            }

            if (returnPath.StartsWith("//", StringComparison.Ordinal) || returnPath.StartsWith(@"/\", StringComparison.Ordinal))
            {
                return false;
            }

            return Uri.TryCreate("https://local.test" + returnPath, UriKind.Absolute, out parsedUri);
        }

        public static bool TryParseLocalReturnUri(Uri returnUri, out Uri parsedUri)
        {
            parsedUri = returnUri;
            return returnUri != null;
        }

        public static string FormatReturnPathAndQuery(Uri returnUri)
        {
            if (returnUri == null)
            {
                return null;
            }

            return returnUri.PathAndQuery;
        }

        public static int? ExtractPositionId(Uri returnUri)
        {
            if (returnUri == null || string.IsNullOrEmpty(returnUri.Query))
            {
                return null;
            }

            var query = HttpUtility.ParseQueryString(returnUri.Query);
            return int.TryParse(query["positionId"], out var positionId) ? (int?)positionId : null;
        }
    }
}
