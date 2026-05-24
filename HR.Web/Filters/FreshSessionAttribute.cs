using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace HR.Web.Filters
{
    public class FreshSessionAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var freshSession = filterContext.HttpContext.Request.QueryString["fresh"];
            if (!string.IsNullOrEmpty(freshSession) && freshSession == "1")
            {
                // Store a flag in session to indicate this is a fresh session request
                if (filterContext.HttpContext.Session != null)
                {
                    filterContext.HttpContext.Session["IsFreshPortalSession"] = true;
                    filterContext.HttpContext.Session["OriginalUserAgent"] = filterContext.HttpContext.Request.UserAgent;
                }

                // Clear authentication cookie for this specific request only
                var authCookie = filterContext.HttpContext.Request.Cookies[FormsAuthentication.FormsCookieName];
                if (authCookie != null)
                {
                    // Create a temporary cookie that expires immediately for this response
                    var tempCookie = new HttpCookie(FormsAuthentication.FormsCookieName, "")
                    {
                        Expires = DateTime.Now.AddSeconds(-1),
                        Path = "/",
                        HttpOnly = true
                    };
                    filterContext.HttpContext.Response.Cookies.Add(tempCookie);
                }

                // Redirect to clean URL without fresh parameter
                var request = filterContext.HttpContext.Request;
                var query = HttpUtility.ParseQueryString(request.QueryString.ToString());
                query.Remove("fresh");

                var path = request.Url != null ? request.Url.AbsolutePath : "/";
                if (string.IsNullOrEmpty(path) || !path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
                {
                    path = "/";
                }

                var queryString = query.ToString();
                var redirectUrl = string.IsNullOrEmpty(queryString) ? path : path + "?" + queryString;

                var urlHelper = new UrlHelper(filterContext.RequestContext);
                if (!urlHelper.IsLocalUrl(redirectUrl))
                {
                    redirectUrl = "/";
                }

                filterContext.Result = new RedirectResult(redirectUrl);
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
