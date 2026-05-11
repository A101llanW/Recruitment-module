using System;
using System.Web;
using System.Web.Mvc;
using HR.Web.Services;

namespace HR.Web.Filters
{
    public class LicenseCheckAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // SAFETY BYPASS: Never run license checks on Account or Captcha
            // Account: prevents infinite loops on LicenseExpired and Login pages
            // Captcha: login AJAX must receive JSON, not a redirect to LicenseExpired
            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            if (string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase)
                || string.Equals(controllerName, "Captcha", StringComparison.OrdinalIgnoreCase))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            using (var uow = new HR.Web.Data.UnitOfWork())
            {
                var tenantService = new TenantService(uow);

                // Check if we are in a company context
                var companyIdValue = tenantService.GetCurrentUserCompanyId();
                if (!companyIdValue.HasValue)
                {
                    // No company context (global portal) - skip license check
                    base.OnActionExecuting(filterContext);
                    return;
                }

                // Verify the company actually exists in the DB to avoid orphaned user errors
                using (var checkUow = new HR.Web.Data.UnitOfWork())
                {
                    var companyExists = checkUow.Companies.Get(companyIdValue.Value);
                    if (companyExists == null)
                    {
                        // Orphaned user! Log them out so they can reset their session
                        System.Web.Security.FormsAuthentication.SignOut();
                        filterContext.HttpContext.Session.Abandon();
                        filterContext.Result = new RedirectResult("~/Account/Login");
                        return;
                    }
                }

                // Skip license check for the system default company
                if (companyIdValue == 1)
                {
                    base.OnActionExecuting(filterContext);
                    return;
                }

                // Check if we are in a company context
                var companyId = tenantService.GetCurrentUserCompanyId();
                if (!companyId.HasValue)
                {
                    // No company context (global portal) - skip license check
                    base.OnActionExecuting(filterContext);
                    return;
                }

                // Skip license check for the system default company or if explicitly allowed
                if (companyId == 1)
                {
                    base.OnActionExecuting(filterContext);
                    return;
                }

                // Check if company license is active
                if (!tenantService.IsCurrentCompanyLicenseActive())
                {
                    filterContext.Result = new RedirectResult("~/Account/LicenseExpired");
                    return;
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
