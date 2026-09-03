using System;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Helpers;
using HR.Web.Services;

namespace HR.Web.Filters
{
    /// <summary>
    /// Ends expired SuperAdmin impersonation sessions server-side and redirects to the global portal.
    /// </summary>
    public class ImpersonationExpiryFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var httpContext = filterContext.HttpContext;
            if (httpContext == null ||
                httpContext.User == null ||
                !httpContext.User.Identity.IsAuthenticated ||
                httpContext.Session == null)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            if (!ImpersonationSessionHelper.IsSessionImpersonating(httpContext.Session))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var tenantService = new TenantService();
            if (!tenantService.IsActualSuperAdmin())
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var actionName = filterContext.ActionDescriptor.ActionName;
            if (IsWhitelisted(controllerName, actionName))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            int? companyId = httpContext.Session["ImpersonatedCompanyId"] as int?;
            using (var uow = new UnitOfWork())
            {
                var request = ImpersonationSessionHelper.GetSessionRequest(httpContext.Session, uow);
                if (ImpersonationSessionHelper.IsRequestActive(request))
                {
                    base.OnActionExecuting(filterContext);
                    return;
                }

                if (request != null)
                {
                    ImpersonationSessionHelper.ExpireRequest(request, uow);
                    uow.Complete();
                }
            }

            ImpersonationSessionHelper.ClearSession(httpContext.Session);

            var auditService = new AuditService();
            auditService.LogAction(
                httpContext.User.Identity.Name,
                "IMPERSONATION_EXPIRED",
                "Companies",
                companyId.HasValue ? companyId.Value.ToString() : null,
                null,
                null);

            filterContext.Result = ImpersonationSessionHelper.BuildSuperAdminPostExpiryRedirect(companyId);
        }

        private static bool IsWhitelisted(string controllerName, string actionName)
        {
            if (string.Equals(controllerName, "Dashboard", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionName, "GetMyImpersonationStatus", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "GetImpersonationStatus", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "GetPendingRequests", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(controllerName, "Companies", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionName, "StopImpersonating", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "Index", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "CompanyDetails", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
