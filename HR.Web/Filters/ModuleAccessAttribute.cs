using System;
using System.Web.Mvc;
using System.Web.Routing;
using HR.Web.Services;

namespace HR.Web.Filters
{
    public class ModuleAccessAttribute : ActionFilterAttribute
    {
        private readonly string _moduleKey;
        private readonly string _requiredAccessLevel;

        public ModuleAccessAttribute()
        {
        }

        public ModuleAccessAttribute(string moduleKey)
        {
            _moduleKey = moduleKey;
        }

        public ModuleAccessAttribute(string moduleKey, string requiredAccessLevel)
        {
            _moduleKey = moduleKey;
            _requiredAccessLevel = requiredAccessLevel;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true) ||
                filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var actionName = filterContext.ActionDescriptor.ActionName;
            var moduleKey = string.IsNullOrWhiteSpace(_moduleKey)
                ? RoleModuleCatalog.ResolveModule(controllerName, actionName)
                : _moduleKey;

            if (string.IsNullOrWhiteSpace(moduleKey))
            {
                filterContext.Result = new HttpStatusCodeResult(
                    403,
                    "Access denied. This action is not mapped to a module permission.");
                return;
            }

            var requiredAccessLevel = string.IsNullOrWhiteSpace(_requiredAccessLevel)
                ? RoleModuleCatalog.ResolveRequiredAccessLevel(filterContext.HttpContext.Request.HttpMethod, actionName)
                : _requiredAccessLevel;

            if (RolePermissionService.CanGuestBrowseModule(moduleKey, requiredAccessLevel))
            {
                var guestUser = filterContext.HttpContext != null ? filterContext.HttpContext.User : null;
                if (guestUser == null || guestUser.Identity == null || !guestUser.Identity.IsAuthenticated)
                {
                    base.OnActionExecuting(filterContext);
                    return;
                }
            }

            var permissionService = new RolePermissionService();
            if (!permissionService.CanCurrentUserAccessModule(moduleKey, requiredAccessLevel))
            {
                var user = filterContext.HttpContext != null ? filterContext.HttpContext.User : null;
                if (user != null && user.Identity != null && user.Identity.IsAuthenticated)
                {
                    var routeValues = new RouteValueDictionary(filterContext.RouteData.Values);
                    routeValues["controller"] = "Home";
                    routeValues["action"] = "Forbidden";
                    var routeName = string.IsNullOrWhiteSpace(routeValues["tenant"] as string) ? "Default" : "Tenant";
                    filterContext.Result = new RedirectToRouteResult(routeName, routeValues);
                    return;
                }

                filterContext.Result = new HttpStatusCodeResult(
                    403,
                    string.Format("Access denied. Your role does not have {0} permission for the {1} module.", requiredAccessLevel, moduleKey));
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
