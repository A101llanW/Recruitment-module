using System.Web.Mvc;
using HR.Web.Helpers;

namespace HR.Web.Filters
{
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
            if (string.IsNullOrEmpty(tenant) && filterContext.HttpContext != null && filterContext.HttpContext.Request != null)
            {
                tenant = TenantAuthRedirectHelper.ExtractTenantSlugFromPath(
                    filterContext.HttpContext.Request.RawUrl,
                    filterContext.HttpContext.Request.ApplicationPath);
            }

            var returnUrl = filterContext.HttpContext != null && filterContext.HttpContext.Request != null
                ? filterContext.HttpContext.Request.RawUrl
                : null;
            var urlHelper = new System.Web.Mvc.UrlHelper(filterContext.RequestContext);
            var loginUrl = urlHelper.Action("Login", "Account", new { tenant = tenant, returnUrl = returnUrl });
            filterContext.Result = new RedirectResult(loginUrl);
        }
    }
}