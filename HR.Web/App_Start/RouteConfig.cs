using System.Web.Mvc;
using System.Web.Routing;
using HR.Web.Helpers;

namespace HR.Web
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            if (routes == null)
            {
                return;
            }

            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("Content/{*pathInfo}");
            routes.IgnoreRoute("Scripts/{*pathInfo}");

            // Orphan URL alias: /{tenant}/CandidateRankings → Admin/CandidateRankings
            routes.MapRoute(
                name: "TenantCandidateRankingsAlias",
                url: "{tenant}/CandidateRankings",
                defaults: new { controller = "Admin", action = "CandidateRankings" },
                constraints: new { tenant = new TenantRouteConstraint() }
            );

            // Tenant-specific branded routes (e.g., /T8k-R2m/positions)
            routes.MapRoute(
                name: "Tenant",
                url: "{tenant}/{controller}/{action}/{id}",
                defaults: new { controller = "Positions", action = "Index", id = UrlParameter.Optional },
                constraints: new { tenant = new TenantRouteConstraint() }
            );

            // SuperAdmin global dashboard (root access)
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Companies", action = "Index", id = UrlParameter.Optional }
            );

            // Keep legacy SuperAdmin route for compatibility
            routes.MapRoute(
                name: "SuperAdminLegacy",
                url: "SuperAdmin/{action}/{id}",
                defaults: new { controller = "Companies", action = "Index", id = UrlParameter.Optional }
            );

            // Fallback for URLs that do not match any route above
            routes.MapRoute(
                name: "NotFound",
                url: "{*catchall}",
                defaults: new { controller = "Error", action = "NotFound" }
            );
        }
    }
}


