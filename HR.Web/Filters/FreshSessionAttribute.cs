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

                // Redirect to clean URL without fresh parameter (route-based, not raw user URL).
                var request = filterContext.HttpContext.Request;
                var routeValues = new System.Web.Routing.RouteValueDictionary(filterContext.RouteData.Values);
                var query = HttpUtility.ParseQueryString(request.QueryString.ToString());
                query.Remove("fresh");
                foreach (string key in query.AllKeys ?? Array.Empty<string>())
                {
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    routeValues[key] = query[key];
                }

                filterContext.Result = new RedirectToRouteResult(routeValues);
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
