using System;
using System.Web.Mvc;
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
                base.OnActionExecuting(filterContext);
                return;
            }

            var requiredAccessLevel = string.IsNullOrWhiteSpace(_requiredAccessLevel)
                ? RoleModuleCatalog.ResolveRequiredAccessLevel(filterContext.HttpContext.Request.HttpMethod, actionName)
                : _requiredAccessLevel;

            var permissionService = new RolePermissionService();
            if (!permissionService.CanCurrentUserAccessModule(moduleKey, requiredAccessLevel))
            {
                filterContext.Result = new HttpStatusCodeResult(
                    403,
                    string.Format("Access denied. Your role does not have {0} permission for the {1} module.", requiredAccessLevel, moduleKey));
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
