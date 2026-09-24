using System;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Filters
{
    /// <summary>
    /// Global filter to automatically log user actions across all controllers.
    /// Captures the controller, action, and success status of every request.
    /// </summary>
    public class AuditLogAttribute : ActionFilterAttribute
    {
        private AuditService _auditService;
        private SecurityService _securityService;

        private AuditService AuditService
        {
            get { return _auditService ?? (_auditService = new AuditService()); }
        }

        private SecurityService SecurityService
        {
            get { return _securityService ?? (_securityService = new SecurityService()); }
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (filterContext.IsChildAction)
            {
                return;
            }

            var request = filterContext.HttpContext.Request;
            var user = filterContext.HttpContext.User;
            var isAuthenticated = user != null && user.Identity != null && user.Identity.IsAuthenticated;
            var username = isAuthenticated ? user.Identity.Name : "Anonymous";

            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var action = filterContext.ActionDescriptor.ActionName;

            if (controller == "Admin" && action == "SecurityLogs")
            {
                return;
            }

            if (controller == "Dashboard" &&
                (action == "GetImpersonationStatus" || action == "GetPendingRequests" || action == "GetMyImpersonationStatus"))
            {
                return;
            }

            var httpMethod = request.HttpMethod;
            var actionType = string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase) ? "VIEW" : httpMethod;
            var actionCode = actionType + ":" + action;
            var isSuccessful = filterContext.Exception == null;
            var technicalError = filterContext.Exception != null ? filterContext.Exception.Message : null;
            var entityId = filterContext.RouteData.Values["id"] != null
                ? filterContext.RouteData.Values["id"].ToString()
                : null;
            var companyId = ResolveTenantCompanyId(filterContext);
            var companyName = ResolveTenantCompanyName(filterContext);

            string friendlySummary;
            if (!isAuthenticated)
            {
                friendlySummary = SecurityLogTranslator.DescribeVisitorActivity(
                    companyName,
                    controller,
                    action,
                    filterContext.RouteData.Values,
                    request);
            }
            else
            {
                friendlySummary = SecurityLogTranslator.DescribeAuditActivity(
                    username,
                    actionCode,
                    controller,
                    entityId,
                    isSuccessful,
                    null,
                    technicalError);
            }

            AuditService.LogAction(
                username,
                actionCode,
                controller,
                entityId,
                wasSuccessful: isSuccessful,
                errorMessage: friendlySummary,
                companyId: companyId);

            if (!isAuthenticated &&
                companyId.HasValue &&
                isSuccessful &&
                string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase) &&
                ShouldLogVisitorAccess(controller, action))
            {
                try
                {
                    SecurityService.RecordVisitorActivity(
                        companyId.Value,
                        request.UserHostAddress,
                        friendlySummary);
                }
                catch (Exception)
                {
                    // Visitor logging must never break page rendering.
                }
            }

            base.OnActionExecuted(filterContext);
        }

        private static int? ResolveTenantCompanyId(ActionExecutedContext filterContext)
        {
            var httpContext = filterContext != null ? filterContext.HttpContext : null;
            if (httpContext == null || !httpContext.Items.Contains("TenantContext"))
            {
                return null;
            }

            var tenantContext = httpContext.Items["TenantContext"];
            if (tenantContext is int companyId)
            {
                return companyId;
            }

            int parsedCompanyId;
            if (tenantContext != null && int.TryParse(tenantContext.ToString(), out parsedCompanyId))
            {
                return parsedCompanyId;
            }

            return null;
        }

        private static string ResolveTenantCompanyName(ActionExecutedContext filterContext)
        {
            if (filterContext == null || filterContext.Controller == null)
            {
                return null;
            }

            var tenantCompany = filterContext.Controller.ViewBag.TenantContext as Company;
            return tenantCompany != null && !string.IsNullOrWhiteSpace(tenantCompany.Name)
                ? tenantCompany.Name.Trim()
                : null;
        }

        private static bool ShouldLogVisitorAccess(string controller, string action)
        {
            if (string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "SecurityLogs", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(controller, "Error", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(controller, "Dashboard", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(controller, "Home", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(action, "Error", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(action, "NotFound", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(action, "Forbidden", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }
    }
}
