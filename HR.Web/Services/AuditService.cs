using System;
using System.Web;
using HR.Web.Data;
using HR.Web.Models;
using Newtonsoft.Json;

namespace HR.Web.Services
{
    public class AuditService
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly TenantService _tenantService = new TenantService();

        public void LogAction(string username, string action, string controller, 
            string entityId = null, object oldValues = null, object newValues = null, 
            bool wasSuccessful = true, string errorMessage = null)
        {
            try
            {
                var context = HttpContext.Current;
                var auditLog = new AuditLog
                {
                    CompanyId = _tenantService.GetCurrentUserCompanyId(), // Add CompanyId for tenant filtering
                    Username = username ?? "Anonymous",
                    Action = action,
                    Controller = controller,
                    EntityId = entityId,
                    OldValues = oldValues != null ? JsonConvert.SerializeObject(oldValues) : null,
                    NewValues = newValues != null ? JsonConvert.SerializeObject(newValues) : null,
                    IPAddress = (context != null && context.Request != null ? context.Request.UserHostAddress : null) ?? "Unknown",
                    Timestamp = DateTime.Now,
                    UserAgent = (context != null && context.Request != null ? context.Request.UserAgent : null) ?? "Unknown",
                    WasSuccessful = wasSuccessful,
                    ErrorMessage = errorMessage
                };

                _uow.AuditLogs.Add(auditLog);
                _uow.Complete();
            }
            catch (Exception)
            {
                // Log audit failures to system event log or file
                // Don't throw exceptions to avoid breaking the main application flow
            }
        }

        public void LogLogin(string username, bool wasSuccessful, string errorMessage = null)
        {
            LogAction(username, wasSuccessful ? "LOGIN_SUCCESS" : "LOGIN_FAILED", "Account", 
                wasSuccessful: wasSuccessful, errorMessage: errorMessage);
        }

        public void LogLogout(string username)
        {
            LogAction(username, "LOGOUT", "Account");
        }

        public void LogCreate(string username, string controller, string entityId, object newValues)
        {
            LogAction(username, "CREATE", controller, entityId: entityId, newValues: newValues);
        }

        public void LogUpdate(string username, string controller, string entityId, object oldValues, object newValues)
        {
            LogAction(username, "UPDATE", controller, entityId: entityId, 
                oldValues: oldValues, newValues: newValues);
        }

        public void LogDelete(string username, string controller, string entityId, object oldValues)
        {
            LogAction(username, "DELETE", controller, entityId: entityId, oldValues: oldValues);
        }

        public void LogView(string username, string controller, string entityId)
        {
            LogAction(username, "VIEW", controller, entityId: entityId);
        }

        public void LogUnauthorizedAccess(string username, string controller, string action)
        {
            LogAction(username, "UNAUTHORIZED_ACCESS", controller, 
                wasSuccessful: false, errorMessage: "Attempted to access " + action);
        }
    }
}
