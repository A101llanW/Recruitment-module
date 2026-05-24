using System;
using System.Collections.Specialized;
using System.Web;
using System.Web.Mvc;
using HR.Web.Helpers;

namespace HR.Web.Controllers
{
    public partial class AccountController
    {
        private ActionResult BuildSafeReturnRedirect(Uri returnUri, string tenantSlug)
        {
            if (returnUri == null)
            {
                return null;
            }

            var segments = SplitPathSegments(returnUri.AbsolutePath);
            if (segments.Length == 0)
            {
                return null;
            }

            ResolveRouteSegments(segments, out var pathTenant, out var controllerSegment, out var actionSegment);
            var resolvedTenant = ResolveTenantSlug(tenantSlug, pathTenant);
            return BuildWhitelistedRedirect(controllerSegment, actionSegment, segments.Length, resolvedTenant, returnUri.Query);
        }

        private static string[] SplitPathSegments(string path)
        {
            return (path ?? string.Empty).Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void ResolveRouteSegments(string[] segments, out string pathTenant, out string controllerSegment, out string actionSegment)
        {
            pathTenant = null;
            if (segments.Length >= 2 && IsTenantAwareController(segments[1]))
            {
                pathTenant = segments[0];
                controllerSegment = segments[1];
                actionSegment = segments.Length >= 3 ? segments[2] : "Index";
                return;
            }

            controllerSegment = segments[0];
            actionSegment = segments.Length >= 2 ? segments[1] : "Index";
        }

        private static bool IsTenantAwareController(string segment)
        {
            return segment.Equals("Applications", StringComparison.OrdinalIgnoreCase) ||
                   segment.Equals("Positions", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveTenantSlug(string tenantSlug, string pathTenant)
        {
            return string.IsNullOrWhiteSpace(tenantSlug) ? pathTenant : tenantSlug;
        }

        private ActionResult BuildWhitelistedRedirect(string controllerSegment, string actionSegment, int segmentLength, string resolvedTenant, string queryString)
        {
            var query = HttpUtility.ParseQueryString(queryString ?? string.Empty);
            if (controllerSegment.Equals("Applications", StringComparison.OrdinalIgnoreCase) &&
                TryBuildApplicationsRedirect(actionSegment, query, resolvedTenant, out var applicationRedirect))
            {
                return applicationRedirect;
            }

            if (IsPositionsIndexRoute(controllerSegment, actionSegment, segmentLength))
            {
                return RedirectToAction("Index", "Positions", new { tenant = resolvedTenant });
            }

            return null;
        }

        private bool TryBuildApplicationsRedirect(string actionSegment, NameValueCollection query, string resolvedTenant, out ActionResult redirectResult)
        {
            redirectResult = null;
            if (!int.TryParse(query["positionId"], out var positionId))
            {
                return false;
            }

            if (actionSegment.Equals("Questionnaire", StringComparison.OrdinalIgnoreCase))
            {
                redirectResult = RedirectToAction("Questionnaire", "Applications", new { tenant = resolvedTenant, positionId = positionId });
                return true;
            }

            if (actionSegment.Equals("Apply", StringComparison.OrdinalIgnoreCase))
            {
                redirectResult = RedirectToAction("Apply", "Applications", new { tenant = resolvedTenant, positionId = positionId });
                return true;
            }

            return false;
        }

        private static bool IsPositionsIndexRoute(string controllerSegment, string actionSegment, int segmentLength)
        {
            if (!controllerSegment.Equals("Positions", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return actionSegment.Equals("Index", StringComparison.OrdinalIgnoreCase) || segmentLength == 1;
        }
    }
}
