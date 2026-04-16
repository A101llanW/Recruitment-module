using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HR.Web.Services;

namespace HR.Web.Filters
{
    public class RoleBasedAuthorizationAttribute : ActionFilterAttribute
    {
        private readonly string[] _allowedRoles;
        private readonly bool _requireGlobalAccess;

        public RoleBasedAuthorizationAttribute(params string[] allowedRoles)
        {
            _allowedRoles = allowedRoles ?? new string[0];
            // Only require global access if SuperAdmin is an allowed role AND Admin is NOT.
            // This ensures that hybrid pages (accessible by both) can be accessed via tenant URLs by Admins.
            _requireGlobalAccess = _allowedRoles.Contains("SuperAdmin") && !_allowedRoles.Contains("Admin");
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true) || 
                filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var tenantService = new TenantService();
            var currentUserRole = tenantService.GetCurrentUserRole() ?? "";
            var tenantToken = filterContext.RouteData.Values["tenant"] as string;

            // Special case: allow actual SuperAdmins to always reach Companies/StopImpersonating
            // even while impersonating, so they can safely exit impersonation without being
            // blocked by tenant/global restrictions or role masking.
            var actionName = filterContext.ActionDescriptor.ActionName;
            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            if (string.Equals(controllerName, "Companies", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actionName, "StopImpersonating", StringComparison.OrdinalIgnoreCase) &&
                tenantService.IsActualSuperAdmin())
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            // 1. Check if this is a "Global Only" page (only SuperAdmins, no Admins allowed)
            // and if it's being accessed via a tenant URL (e.g. /company/Admin/...)
            if (_requireGlobalAccess && !string.IsNullOrEmpty(tenantToken))
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Access denied. System-wide management functions (SuperAdmin) must be accessed from the global portal, not via a company-branded URL.");
                return;
            }

            // 2. Role-based check
            if (_allowedRoles.Length > 0)
            {
                bool hasRequiredRole = _allowedRoles.Any(role => currentUserRole.Equals(role, StringComparison.OrdinalIgnoreCase));

                // Special case: If the user is a SuperAdmin, they should generally be allowed in any role check
                if (tenantService.IsSuperAdmin()) hasRequiredRole = true;

                if (!hasRequiredRole)
                {
                    filterContext.Result = new HttpStatusCodeResult(403, string.Format("Access denied. Your role '{0}' does not have permission to access this page. Required roles: {1}.", currentUserRole, string.Join(", ", _allowedRoles)));
                    return;
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
