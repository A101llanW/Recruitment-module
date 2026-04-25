using System;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class DashboardController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly AuditService _auditService = new AuditService();
        private readonly TenantService _tenantService = new TenantService();

        public ActionResult Index()
        {
            var positionsQuery = _uow.Positions.GetAll().AsQueryable();
            var applicationsQuery = _uow.Applications.GetAll().AsQueryable();
            var interviewsQuery = _uow.Interviews.GetAll().AsQueryable();
            var usersQuery = _uow.Users.GetAll().AsQueryable();

            // Apply tenant filtering to all dashboard metrics
            positionsQuery = _tenantService.ApplyTenantFilter(positionsQuery);
            applicationsQuery = _tenantService.ApplyTenantFilter(applicationsQuery);
            interviewsQuery = _tenantService.ApplyTenantFilter(interviewsQuery);
            usersQuery = _tenantService.ApplyTenantFilter(usersQuery);

            var openPositions = positionsQuery.Count(p => p.IsOpen);
            var pendingApplications = applicationsQuery.Count(a => a.Status == "Interviewing");
            var scheduledInterviews = interviewsQuery.Count();
            var totalUsers = usersQuery.Count();

            ViewBag.OpenPositions = openPositions;
            ViewBag.PendingApplications = pendingApplications;
            ViewBag.ScheduledInterviews = scheduledInterviews;
            ViewBag.TotalUsers = totalUsers;

            ViewBag.PendingImpersonationRequests = _uow.ImpersonationRequests.GetAll()
                .Where(r => r.RequestedFrom == User.Identity.Name && r.Status == ImpersonationRequestStatus.Pending)
                .ToList();

            return View();
        }

        public JsonResult GetPendingRequests()
        {
            if (!User.IsInRole("Admin")) return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);

            var requests = _uow.ImpersonationRequests.GetAll()
                .Where(r => r.RequestedFrom == User.Identity.Name && r.Status == ImpersonationRequestStatus.Pending)
                .ToList() // Execute query first
                .Select(r => new {
                    id = r.Id,
                    requestedBy = r.RequestedBy,
                    requestDate = r.RequestDate.ToString("HH:mm dd MMM yyyy"),
                    reason = r.Reason
                })
                .ToList();

            return Json(new { count = requests.Count, requests = requests }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetImpersonationStatus()
        {
            var user = GetImpersonationScopeUser();
            if (user == null || !user.CompanyId.HasValue)
            {
                return Json(new { isLocked = false }, JsonRequestBehavior.AllowGet);
            }

            ExpireStaleImpersonationRequests(user.CompanyId.Value);
            var activeImpersonation = GetActiveImpersonationRequest(user.CompanyId.Value);
            return BuildImpersonationStatusResponse(activeImpersonation);
        }

        public JsonResult GetMyImpersonationStatus()
        {
            if (!User.Identity.IsAuthenticated) return Json(new { secondsLeft = 0 }, JsonRequestBehavior.AllowGet);
            
            var requestId = Session["ImpersonatedRequestId"] as int?;
            if (!requestId.HasValue) return Json(new { secondsLeft = 0 }, JsonRequestBehavior.AllowGet);
            
            var request = _uow.ImpersonationRequests.Get(requestId.Value);
            if (request == null || (request.Status != ImpersonationRequestStatus.Active && request.Status != ImpersonationRequestStatus.Approved)) 
                return Json(new { secondsLeft = 0 }, JsonRequestBehavior.AllowGet);
                
            var secondsLeft = 0;
            if (request.ExpiryDate.HasValue)
            {
                secondsLeft = (int)(request.ExpiryDate.Value - DateTime.Now).TotalSeconds;
            }
            
            return Json(new { secondsLeft = Math.Max(0, secondsLeft) }, JsonRequestBehavior.AllowGet);
        }

        [Authorize(Roles = "SuperAdmin")]
        public ActionResult Rescue()
        {
            // CLEAR ALL ACTIVE OR APPROVED SESSIONS NATIONWIDE
            var activeRequests = _uow.ImpersonationRequests.GetAll()
                .Where(r => r.Status == ImpersonationRequestStatus.Active || r.Status == ImpersonationRequestStatus.Approved)
                .ToList();

            foreach (var req in activeRequests)
            {
                req.Status = ImpersonationRequestStatus.Expired;
                _uow.ImpersonationRequests.Update(req);
            }

            _uow.Complete();
            
            // Wipe Global session tracking
            Session.Clear(); 
            Session.Abandon();

            _auditService.LogAction(User.Identity.Name, "SYSTEM_RESCUE", "Account", null, true, "SuperAdmin triggered emergency session wipe");
            return Content("SYSTEM RESCUED: Cleared " + activeRequests.Count + " sessions. Please log in again.");
        }

        public JsonResult CheckRequestStatus(int requestId)
        {
            var request = _uow.ImpersonationRequests.Get(requestId);
            if (request == null) return Json(new { status = "NotFound" }, JsonRequestBehavior.AllowGet);

            return Json(new { status = request.Status.ToString() }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HandleImpersonationRequest(int requestId, bool approved, int? durationMinutes = null, string notes = null)
        {
            var request = _uow.ImpersonationRequests.Get(requestId);
            if (!CanHandleImpersonationRequest(request))
            {
                return HttpNotFound();
            }

            if (request.Status == ImpersonationRequestStatus.Pending)
            {
                ApplyImpersonationDecision(request, approved, durationMinutes, notes);
                SaveImpersonationDecision(request);
                LogImpersonationDecision(request, requestId, approved, durationMinutes);
                SetImpersonationDecisionFeedback(request, approved, durationMinutes, notes);
            }

            return RedirectToAction("Index");
        }

        private User GetImpersonationScopeUser()
        {
            if (!User.Identity.IsAuthenticated || User.IsInRole("SuperAdmin") || !User.IsInRole("Admin"))
            {
                return null;
            }

            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            return _uow.Users.GetAll().FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
        }

        private void ExpireStaleImpersonationRequests(int companyId)
        {
            var now = DateTime.Now;
            var staleRequests = _uow.ImpersonationRequests.GetAll()
                .Where(r => r.CompanyId == companyId &&
                    (r.Status == ImpersonationRequestStatus.Active || r.Status == ImpersonationRequestStatus.Approved) &&
                    r.ExpiryDate.HasValue &&
                    r.ExpiryDate < now)
                .ToList();

            if (!staleRequests.Any())
            {
                return;
            }

            foreach (var staleRequest in staleRequests)
            {
                staleRequest.Status = ImpersonationRequestStatus.Expired;
            }

            _uow.Complete();
        }

        private ImpersonationRequest GetActiveImpersonationRequest(int companyId)
        {
            return _uow.ImpersonationRequests.GetAll()
                .Where(r => r.CompanyId == companyId && r.Status == ImpersonationRequestStatus.Active)
                .OrderByDescending(r => r.ExpiryDate)
                .FirstOrDefault();
        }

        private JsonResult BuildImpersonationStatusResponse(ImpersonationRequest activeImpersonation)
        {
            if (activeImpersonation == null)
            {
                return Json(new { isLocked = false }, JsonRequestBehavior.AllowGet);
            }

            var secondsLeft = activeImpersonation.ExpiryDate.HasValue
                ? (int)(activeImpersonation.ExpiryDate.Value - DateTime.Now).TotalSeconds
                : 3600;

            return Json(
                new
                {
                    isLocked = true,
                    expiry = activeImpersonation.ExpiryDate.HasValue ? activeImpersonation.ExpiryDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                    secondsLeft = Math.Max(0, secondsLeft)
                },
                JsonRequestBehavior.AllowGet);
        }

        private bool CanHandleImpersonationRequest(ImpersonationRequest request)
        {
            return request != null && request.RequestedFrom == User.Identity.Name;
        }

        private static void ApplyImpersonationDecision(ImpersonationRequest request, bool approved, int? durationMinutes, string notes)
        {
            request.Status = approved ? ImpersonationRequestStatus.Approved : ImpersonationRequestStatus.Rejected;
            request.DecisionDate = DateTime.Now;
            request.AdminNotes = notes;

            if (approved)
            {
                var minutes = durationMinutes ?? 60;
                request.ExpiryDate = DateTime.Now.AddMinutes(minutes);
            }
        }

        private void SaveImpersonationDecision(ImpersonationRequest request)
        {
            _uow.ImpersonationRequests.Update(request);
            _uow.Complete();
        }

        private void LogImpersonationDecision(ImpersonationRequest request, int requestId, bool approved, int? durationMinutes)
        {
            _auditService.LogAction(
                User.Identity.Name,
                approved ? "IMPERSONATION_APPROVED" : "IMPERSONATION_REJECTED",
                "Dashboard",
                requestId.ToString(),
                null,
                new
                {
                    SuperAdmin = request.RequestedBy,
                    Outcome = request.Status.ToString(),
                    CompanyId = request.CompanyId,
                    Duration = durationMinutes
                });
        }

        private void SetImpersonationDecisionFeedback(ImpersonationRequest request, bool approved, int? durationMinutes, string notes)
        {
            TempData["SuccessMessage"] = approved
                ? string.Format("Elevation request approved for {0} minutes.", durationMinutes ?? 60)
                : "Elevation request rejected.";

            if (approved)
            {
                return;
            }

            Session["RejectionNotification"] = new
            {
                RequestedBy = request.RequestedBy,
                RequestedFrom = request.RequestedFrom,
                Reason = request.Reason,
                AdminNotes = notes,
                DecisionDate = request.DecisionDate,
                CompanyId = request.CompanyId
            };
        }
    }
}






