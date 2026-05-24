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
        public static bool TryParseLocalReturnUri(string returnUrl, UrlHelper urlHelper, out Uri parsedUri)
        {
            parsedUri = null;
            if (string.IsNullOrWhiteSpace(returnUrl) || urlHelper == null || !urlHelper.IsLocalUrl(returnUrl))
            {
                return false;
            }

            if (returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.StartsWith(@"/\", StringComparison.Ordinal))
            {
                return false;
            }

            return Uri.TryCreate("https://local.test" + returnUrl, UriKind.Absolute, out parsedUri);
        }

        public static string ToReturnUrlString(Uri returnUri)
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
