using System;
using System.Web;

namespace HR.Web.Helpers
{
    public static class UrlHelper
    {
        /// <summary>
        /// Gets the base URI for external access.
        /// Checks Web.config ExternalBaseUrl first, then falls back to the current request URL.
        /// </summary>
        public static Uri GetBaseUri(HttpRequestBase request)
        {
            return ExternalUrlHelper.GetBaseUri(request);
        }

        /// <summary>
        /// Gets the base URI using HttpRequest (non-base version).
        /// </summary>
        public static Uri GetBaseUri(HttpRequest request)
        {
            return ExternalUrlHelper.GetBaseUri(request);
        }
    }
}
