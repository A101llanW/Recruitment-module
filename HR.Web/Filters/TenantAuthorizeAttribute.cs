using System.Web.Mvc;

namespace HR.Web.Filters
{
    /// <summary>
    /// Preserves tenant slug and return URL when redirecting unauthenticated users to login.
    /// </summary>
    public class TenantAuthorizeAttribute : AuthorizeAttribute
    {
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                return;
            }

            if (filterContext.HttpContext != null &&
                filterContext.HttpContext.User != null &&
                filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                filterContext.Result = new HttpUnauthorizedResult();
                return;
            }

            var tenant = filterContext.RouteData.Values["tenant"] as string;
            var returnUrl = filterContext.HttpContext != null && filterContext.HttpContext.Request != null
                ? filterContext.HttpContext.Request.RawUrl
                : null;
            var urlHelper = new UrlHelper(filterContext.RequestContext);
            var loginUrl = urlHelper.Action("Login", "Account", new { tenant = tenant, returnUrl = returnUrl });
            filterContext.Result = new RedirectResult(loginUrl);
        }
    }
}