using System.Web;

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
            return ExternalUrlHelper.GetBaseUrl(request);
        }

        /// <summary>
        /// Gets the base URL using HttpRequest (non-base version)
        /// </summary>
        public static string GetBaseUrl(HttpRequest request)
        {
            return ExternalUrlHelper.GetBaseUrl(request);
        }
    }
}
