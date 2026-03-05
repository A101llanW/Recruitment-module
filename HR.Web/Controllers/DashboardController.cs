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
            // Only non-SuperAdmins (regular company admins) should be frozen
            if (!User.Identity.IsAuthenticated || User.IsInRole("SuperAdmin")) 
                return Json(new { isLocked = false }, JsonRequestBehavior.AllowGet);

            if (!User.IsInRole("Admin")) 
                return Json(new { isLocked = false }, JsonRequestBehavior.AllowGet);

            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            var user = _uow.Users.GetAll().FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
            if (user == null || !user.CompanyId.HasValue) return Json(new { isLocked = false }, JsonRequestBehavior.AllowGet);

            // Proactively expire any stale sessions
            var now = DateTime.Now;
            var staleRequests = _uow.ImpersonationRequests.GetAll()
                .Where(r => r.CompanyId == user.CompanyId && 
                       (r.Status == ImpersonationRequestStatus.Active || r.Status == ImpersonationRequestStatus.Approved) &&
                       r.ExpiryDate.HasValue && r.ExpiryDate < now)
                .ToList();

            if (staleRequests.Any())
            {
                foreach(var sr in staleRequests) sr.Status = ImpersonationRequestStatus.Expired;
                _uow.Complete();
            }

            // Check if there is an ACTIVE impersonation request for this company
            var activeImpersonation = _uow.ImpersonationRequests.GetAll()
                .Where(r => r.CompanyId == user.CompanyId && r.Status == ImpersonationRequestStatus.Active)
                .OrderByDescending(r => r.ExpiryDate)
                .FirstOrDefault();

            if (activeImpersonation != null)
            {
                var secondsLeft = activeImpersonation.ExpiryDate.HasValue 
                    ? (int)(activeImpersonation.ExpiryDate.Value - DateTime.Now).TotalSeconds 
                    : 3600; 

                return Json(new { 
                    isLocked = true, 
                    expiry = activeImpersonation.ExpiryDate.HasValue ? activeImpersonation.ExpiryDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                    secondsLeft = Math.Max(0, secondsLeft)
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { isLocked = false }, JsonRequestBehavior.AllowGet);
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

        [AllowAnonymous]
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
            if (request == null || request.RequestedFrom != User.Identity.Name) return HttpNotFound();

            if (request.Status == ImpersonationRequestStatus.Pending)
            {
                request.Status = approved ? ImpersonationRequestStatus.Approved : ImpersonationRequestStatus.Rejected;
                request.DecisionDate = DateTime.Now;
                request.AdminNotes = notes;

                if (approved)
                {
                    // If duration is provided, use it. Otherwise default to 60 minutes for safety.
                    int minutes = durationMinutes ?? 60;
                    request.ExpiryDate = DateTime.Now.AddMinutes(minutes);
                }

                _uow.ImpersonationRequests.Update(request);
                _uow.Complete();

                _auditService.LogAction(
                    User.Identity.Name,
                    approved ? "IMPERSONATION_APPROVED" : "IMPERSONATION_REJECTED",
                    "Dashboard",
                    requestId.ToString(),
                    null,
                    new { 
                        SuperAdmin = request.RequestedBy, 
                        Outcome = request.Status.ToString(),
                        CompanyId = request.CompanyId,
                        Duration = durationMinutes
                    }
                );

                TempData["SuccessMessage"] = approved ? string.Format("Elevation request approved for {0} minutes.", durationMinutes ?? 60) : "Elevation request rejected.";
                
                // If rejected, set a session notification for the SuperAdmin to see when they visit the company
                if (!approved)
                {
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

            return RedirectToAction("Index");
        }
    }
}







