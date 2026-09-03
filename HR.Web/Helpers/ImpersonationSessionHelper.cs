using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using HR.Web.Data;
using MvcUrlHelper = System.Web.Mvc.UrlHelper;
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

        public static bool IsSessionImpersonating(HttpSessionStateBase session)
        {
            return session != null && session["ImpersonatedCompanyId"] != null;
        }

        public static ImpersonationRequest GetSessionRequest(HttpSessionStateBase session, UnitOfWork uow)
        {
            if (session == null || uow == null)
            {
                return null;
            }

            var requestId = session["ImpersonatedRequestId"] as int?;
            if (!requestId.HasValue)
            {
                return null;
            }

            return uow.ImpersonationRequests.Get(requestId.Value);
        }

        public static bool IsRequestActive(ImpersonationRequest request)
        {
            return request != null && !IsRequestExpired(request);
        }

        public static bool IsRequestExpired(ImpersonationRequest request)
        {
            if (request == null)
            {
                return true;
            }

            if (request.Status != ImpersonationRequestStatus.Active &&
                request.Status != ImpersonationRequestStatus.Approved)
            {
                return true;
            }

            if (request.ExpiryDate.HasValue && request.ExpiryDate.Value < DateTime.Now)
            {
                return true;
            }

            return false;
        }

        public static void ClearSession(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return;
            }

            session.Remove("ImpersonatedRequestId");
            session.Remove("ImpersonatedCompanyId");
            session.Remove("ImpersonationReason");
            session.Remove("ImpersonatedCompanyName");
            session.Remove("ImpersonationExpiry");
        }

        public static void ExpireRequest(ImpersonationRequest request, UnitOfWork uow)
        {
            if (request == null || uow == null)
            {
                return;
            }

            if (request.Status == ImpersonationRequestStatus.Active ||
                request.Status == ImpersonationRequestStatus.Approved)
            {
                request.Status = ImpersonationRequestStatus.Expired;
                uow.ImpersonationRequests.Update(request);
            }
        }

        public static void ExpireStaleImpersonationRequestsForCompany(int companyId, UnitOfWork uow)
        {
            if (uow == null)
            {
                return;
            }

            var now = DateTime.Now;
            var staleRequests = uow.ImpersonationRequests.GetAll()
                .Where(r => r.CompanyId == companyId &&
                    r.ExpiryDate.HasValue &&
                    r.ExpiryDate < now &&
                    (r.StatusValue == (int)ImpersonationRequestStatus.Active ||
                     r.StatusValue == (int)ImpersonationRequestStatus.Approved))
                .ToList();

            if (!staleRequests.Any())
            {
                return;
            }

            foreach (var staleRequest in staleRequests)
            {
                staleRequest.Status = ImpersonationRequestStatus.Expired;
                uow.ImpersonationRequests.Update(staleRequest);
            }

            uow.Complete();
        }

        public static ActionResult BuildSuperAdminPostExpiryRedirect(int? companyId)
        {
            if (companyId.HasValue)
            {
                return new RedirectToRouteResult("Default", new RouteValueDictionary
                {
                    { "controller", "Companies" },
                    { "action", "CompanyDetails" },
                    { "id", companyId.Value }
                });
            }

            return new RedirectToRouteResult("Default", new RouteValueDictionary
            {
                { "controller", "Companies" },
                { "action", "Index" }
            });
        }

        public static string BuildSuperAdminPostExpiryUrl(MvcUrlHelper url, int? companyId)
        {
            if (url == null)
            {
                return companyId.HasValue
                    ? string.Format("/Companies/CompanyDetails/{0}", companyId.Value)
                    : "/Companies/Index";
            }

            var routeValues = companyId.HasValue
                ? new { controller = "Companies", action = "CompanyDetails", id = companyId.Value }
                : (object)new { controller = "Companies", action = "Index" };

            var generated = url.RouteUrl("Default", routeValues);
            if (!string.IsNullOrEmpty(generated) && url.IsLocalUrl(generated))
            {
                return generated;
            }

            return companyId.HasValue
                ? string.Format("/Companies/CompanyDetails/{0}", companyId.Value)
                : "/Companies/Index";
        }

        public static string BuildTenantAdminPostUnlockUrl(MvcUrlHelper url, Company company)
        {
            if (url == null)
            {
                return "/Dashboard/Index";
            }

            if (company != null && !string.IsNullOrWhiteSpace(company.Slug))
            {
                return url.RouteUrl("Tenant", new { tenant = company.Slug, controller = "Dashboard", action = "Index" });
            }

            return url.RouteUrl("Default", new { controller = "Dashboard", action = "Index" });
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
                .Where(r => r.RequestedBy == username &&
                    r.CompanyId.HasValue &&
                    r.StatusValue == (int)ImpersonationRequestStatus.Active)
                .OrderByDescending(r => r.DecisionDate ?? r.RequestDate)
                .FirstOrDefault();

            if (request == null)
            {
                return false;
            }

            if (IsRequestExpired(request))
            {
                ExpireRequest(request, uow);
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
