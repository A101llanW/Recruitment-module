using System;
using System.Linq;
using System.Web;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Helpers
{
    public static class ImpersonationSessionHelper
    {
        public static void ApplySession(HttpSessionStateBase session, ImpersonationRequest request, Company company)
        {
            session["ImpersonatedRequestId"] = request.Id;
            session["ImpersonatedCompanyId"] = request.CompanyId;
            session["ImpersonationReason"] = request.Reason ?? "Not specified";
            session["ImpersonatedCompanyName"] = company.Name;
            session["ImpersonationExpiry"] = request.ExpiryDate;
        }

        /// <summary>
        /// Restores an in-progress impersonation after the SuperAdmin logs back in,
        /// as long as the company admin's authorization window has not expired.
        /// </summary>
        public static bool TryRestoreAfterLogout(string username, HttpSessionStateBase session, UnitOfWork uow, AuditService auditService)
        {
            if (string.IsNullOrWhiteSpace(username) || session == null)
            {
                return false;
            }

            var request = uow.ImpersonationRequests.GetAll()
                .Where(r =>
                    r.RequestedBy == username &&
                    r.Status == ImpersonationRequestStatus.Active &&
                    r.CompanyId.HasValue)
                .OrderByDescending(r => r.DecisionDate ?? r.RequestDate)
                .FirstOrDefault();

            if (request == null)
            {
                return false;
            }

            if (request.ExpiryDate.HasValue && request.ExpiryDate.Value < DateTime.Now)
            {
                request.Status = ImpersonationRequestStatus.Expired;
                uow.ImpersonationRequests.Update(request);
                uow.Complete();
                return false;
            }

            var company = uow.Companies.Get(request.CompanyId.Value);
            if (company == null)
            {
                return false;
            }

            ApplySession(session, request, company);

            auditService.LogAction(
                username,
                "IMPERSONATION_RESUME",
                "Account",
                request.CompanyId.ToString(),
                null,
                new { Reason = request.Reason, CompanyName = company.Name, ApprovedBy = request.RequestedFrom });

            return true;
        }
    }
}
