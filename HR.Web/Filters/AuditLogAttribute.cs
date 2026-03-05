using System;
using System.Web.Mvc;
using HR.Web.Services;
using System.Linq;

namespace HR.Web.Filters
{
    /// <summary>
    /// Global filter to automatically log user actions across all controllers.
    /// Captures the controller, action, and success status of every request.
    /// </summary>
    public class AuditLogAttribute : ActionFilterAttribute
    {
        private readonly AuditService _auditService = new AuditService();

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            // Skip child actions to avoid duplicate logs for partial views
            if (filterContext.IsChildAction) return;

            var request = filterContext.HttpContext.Request;
            var user = filterContext.HttpContext.User;
            var username = user.Identity.IsAuthenticated ? user.Identity.Name : "Anonymous";

            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var action = filterContext.ActionDescriptor.ActionName;
            
            // Determine the "Action Type" based on HTTP method or specific logic
            string actionType = request.HttpMethod;
            bool isSuccessful = filterContext.Exception == null;
            string errorMsg = filterContext.Exception?.Message;

            // We don't want to log the "SecurityLogs" page itself to avoid infinite growth when viewing logs
            if (controller == "Admin" && action == "SecurityLogs") return;

            // Exclude background polling requests for impersonation and elevation
            if (controller == "Dashboard" && (action == "GetImpersonationStatus" || action == "GetPendingRequests" || action == "GetMyImpersonationStatus")) return;

            // Optional: Filter out heavy GET requests that are just navigations if needed, 
            // but for a full audit, we log them as "VIEW"
            if (actionType == "GET")
            {
                actionType = "VIEW";
            }

            // Capture Entity ID if it's in the route data (common pattern in this app)
            string entityId = filterContext.RouteData.Values["id"]?.ToString();

            // Log the action
            _auditService.LogAction(
                username, 
                actionType + ":" + action, 
                controller, 
                entityId, 
                wasSuccessful: isSuccessful, 
                errorMessage: errorMsg
            );

            base.OnActionExecuted(filterContext);
        }
    }
}
