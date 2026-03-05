using System.Web.Mvc;

namespace HR.Web
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            
            // Fresh session handler for anonymous portal access
            filters.Add(new HR.Web.Filters.FreshSessionAttribute());
            
            // Fresh portal security to prevent admin access in fresh sessions
            filters.Add(new HR.Web.Filters.FreshPortalSecurityAttribute());
            
            // Global license check for multi-tenant enforcement
            filters.Add(new HR.Web.Filters.LicenseCheckAttribute());

            // Global tenant context resolution
            filters.Add(new HR.Web.Helpers.TenantFilterAttribute());

            // Prevent browser caching for back-button security
            filters.Add(new HR.Web.Filters.NoCacheAttribute());
            
            // Global authentication is removed to allow anonymous browsing.
            // controllers/actions will specify their own [Authorize] requirements.
            // filters.Add(new AuthorizeAttribute());
        }
    }
}


