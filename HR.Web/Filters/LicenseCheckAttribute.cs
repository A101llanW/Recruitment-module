using System;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Services;

namespace HR.Web.Filters
{
    public class LicenseCheckAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (ShouldBypassLicenseCheck(filterContext))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            using (var uow = new HR.Web.Data.UnitOfWork())
            {
                var tenantService = new TenantService(uow);
                var companyIdValue = tenantService.GetCurrentUserCompanyId();
                if (!companyIdValue.HasValue)
                {
                    base.OnActionExecuting(filterContext);
                    return;
                }

                if (TrySignOutOrphanedCompanyUser(filterContext, companyIdValue.Value))
                {
                    return;
                }

                if (companyIdValue == 1)
                {
                    base.OnActionExecuting(filterContext);
                    return;
                }

                if (!tenantService.IsCurrentCompanyLicenseActive())
                {
                    filterContext.Result = new RedirectResult("~/Account/LicenseExpired");
                    return;
                }
            }

            base.OnActionExecuting(filterContext);
        }

        private static bool ShouldBypassLicenseCheck(ActionExecutingContext filterContext)
        {
            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            return string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase)
                || string.Equals(controllerName, "Captcha", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TrySignOutOrphanedCompanyUser(ActionExecutingContext filterContext, int companyId)
        {
            using (var checkUow = new HR.Web.Data.UnitOfWork())
            {
                if (checkUow.Companies.Get(companyId) != null)
                {
                    return false;
                }
            }

            System.Web.Security.FormsAuthentication.SignOut();
            filterContext.HttpContext.Session.Abandon();
            var tenant = filterContext.RouteData.Values["tenant"] as string;
            var returnUrl = filterContext.HttpContext.Request != null ? filterContext.HttpContext.Request.RawUrl : null;
            var urlHelper = new System.Web.Mvc.UrlHelper(filterContext.RequestContext);
            filterContext.Result = new RedirectResult(urlHelper.Action("Login", "Account", new { tenant = tenant, returnUrl = returnUrl }));
            return true;
        }
    }
}
