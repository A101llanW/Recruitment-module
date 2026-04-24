using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace HR.Web.Filters
{
    /// <summary>
    /// Handles stale anti-forgery token failures gracefully when session/auth state has changed.
    /// </summary>
    public class AntiForgeryExceptionFilterAttribute : FilterAttribute, IExceptionFilter
    {
        private const string AntiForgeryCookieName = "__RequestVerificationToken";

        public void OnException(ExceptionContext filterContext)
        {
            if (filterContext == null || filterContext.ExceptionHandled)
            {
                return;
            }

            if (!(filterContext.Exception is HttpAntiForgeryException))
            {
                return;
            }

            var httpContext = filterContext.HttpContext;
            if (httpContext == null)
            {
                return;
            }

            // Ensure any stale auth/anti-forgery state is removed before redirecting.
            FormsAuthentication.SignOut();
            ExpireCookie(httpContext, FormsAuthentication.FormsCookieName);
            ExpireCookie(httpContext, AntiForgeryCookieName);

            filterContext.ExceptionHandled = true;
            httpContext.Response.TrySkipIisCustomErrors = true;

            if (httpContext.Request.IsAjaxRequest())
            {
                httpContext.Response.StatusCode = 401;
                filterContext.Result = new JsonResult
                {
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    Data = new
                    {
                        success = false,
                        reason = "session_expired",
                        message = "Your session has expired. Please sign in and try again."
                    }
                };
                return;
            }

            var controller = filterContext.Controller as Controller;
            if (controller != null)
            {
                controller.TempData["ErrorMessage"] = "Your session expired. Please sign in and try again.";
            }

            filterContext.Result = new RedirectToRouteResult(
                new System.Web.Routing.RouteValueDictionary
                {
                    { "controller", "Account" },
                    { "action", "Login" },
                    { "reason", "session_expired" }
                });
        }

        private static void ExpireCookie(HttpContextBase httpContext, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var expiredCookie = new HttpCookie(name, string.Empty)
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true,
                Secure = httpContext.Request != null && httpContext.Request.IsSecureConnection
            };
            httpContext.Response.Cookies.Add(expiredCookie);
        }
    }
}
