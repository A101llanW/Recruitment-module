using System;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;
using System.Data.Entity;

namespace HR.Web.Controllers
{
    public class InterviewsController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly IEmailService _email = new EmailService();
        private readonly AuditService _auditService = new AuditService();
        private readonly TenantService _tenantService = new TenantService();

        public ActionResult Index()
        {
            if (User == null || !User.Identity.IsAuthenticated)
            {
                ViewBag.Message = "Please sign in or create account first to view your interviews.";
                return View("GuestAccess");
            }

            var user = GetCurrentInterviewUser();
            if (user == null)
            {
                return View(Enumerable.Empty<Interview>());
            }

            if (IsManagementUser(user))
            {
                return View(GetManagementInterviews().ToList());
            }

            return View(GetApplicantInterviews(user).ToList());
        }

        private User GetCurrentInterviewUser()
        {
            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            return _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
        }

        private bool IsManagementUser(User user)
        {
            return User.IsInRole("Admin") ||
                User.IsInRole("SuperAdmin") ||
                user.Role == "Admin" ||
                user.Role == "SuperAdmin";
        }

        private IQueryable<Interview> GetManagementInterviews()
        {
            var items = _uow.Context.Interviews
                .Include("Application.Applicant")
                .Include("Application.Position")
                .Include("Interviewer")
                .AsQueryable();

            return _tenantService.ApplyTenantFilter(items);
        }

        private IQueryable<Interview> GetApplicantInterviews(User user)
        {
            var applicant = _uow.Context.Applicants.FirstOrDefault(a => a.Email == user.Email);
            if (applicant == null)
            {
                return Enumerable.Empty<Interview>().AsQueryable();
            }

            var items = _uow.Context.Interviews
                .Include("Application.Applicant")
                .Include("Application.Position")
                .Include("Interviewer")
                .Where(i => i.Application.ApplicantId == applicant.Id)
                .AsQueryable();

            return _tenantService.ApplyTenantFilter(items);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult BookInterview(int applicationId, int interviewerId, DateTime scheduledAt, string mode)
        {
            try
            {
                // Verify application belongs to tenant
                var application = _uow.Applications.Get(applicationId);
                if (application == null) return HttpNotFound();
                
                var companyId = _tenantService.GetCurrentUserCompanyId();
                if (companyId.HasValue && application.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
                {
                   return new HttpStatusCodeResult(403, "Access Denied");
                }

                var interview = new Interview
                {
                    ApplicationId = applicationId,
                    InterviewerId = interviewerId,
                    ScheduledAt = scheduledAt,
                    Mode = mode,
                    CompanyId = companyId // Inherit company from context
                };
                _uow.Interviews.Add(interview);
                _uow.Complete();
                
                // Log interview booking
                var newValues = new { 
                    ApplicationId = applicationId,
                    InterviewerId = interviewerId,
                    ScheduledAt = scheduledAt,
                    Mode = mode
                };
                _auditService.LogCreate(User.Identity.Name, "Interviews", interview.Id.ToString(), newValues);
                
                var interviewer = _uow.Users.Get(interviewerId);
                if (interviewer != null)
                {
                    _email.SendAsync(interviewer.Email, "Interview scheduled", "You have a new interview scheduled.");
                }
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _auditService.LogAction(User.Identity.Name, "CREATE", "Interviews", "new", 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                TempData["Error"] = "Error booking interview: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [Authorize]
        public ActionResult Details(int id)
        {
            var interview = _uow.Interviews.GetAll(i => i.Application.Applicant, i => i.Application.Position, i => i.Interviewer)
                .FirstOrDefault(i => i.Id == id);
            if (interview == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && interview.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return View(interview);
        }

        [Authorize]
        public ActionResult Create(int? applicationId)
        {
            LoadLookups();
            var interview = new Interview { ScheduledAt = DateTime.UtcNow.AddDays(1) };
            if (applicationId.HasValue)
            {
                interview.ApplicationId = applicationId.Value;
                ViewBag.ApplicationId = new SelectList(_uow.Applications.GetAll(a => a.Applicant, a => a.Position), "Id", "Id", applicationId.Value);
            }
            return View(interview);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Interview model)
        {
            if (!ModelState.IsValid)
            {
                LoadLookups(model);
                return View(model);
            }
            // Assign company
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue)
            {
                model.CompanyId = companyId.Value;
            }

            _uow.Interviews.Add(model);
            _uow.Complete();
            var interviewerEmail = model != null && model.Interviewer != null ? model.Interviewer.Email : null;
            _email.SendAsync(interviewerEmail, "Interview scheduled", "Please attend.");
            return RedirectToAction("Index");
        }

        [Authorize]
        public ActionResult Edit(int id)
        {
            var interview = _uow.Interviews.Get(id);
            if (interview == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && interview.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            LoadLookups(interview);
            return View(interview);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Interview model)
        {
            if (!ModelState.IsValid)
            {
                LoadLookups(model);
                return View(model);
            }

            // Verify ownership
            var existing = _uow.Interviews.Get(model.Id);
            if (existing == null) return HttpNotFound();

            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && existing.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }
            
            // Re-assign basic properties but keep critical ones or use dedicated update logic if needed
            // For now, assume model binding is safe enough if we validate ownership, but ideally fetch and update
            existing.ScheduledAt = model.ScheduledAt;
            existing.Mode = model.Mode;
            existing.Notes = model.Notes;
            existing.InterviewerId = model.InterviewerId;
            // Don't change ApplicationId or CompanyId usually

            _uow.Interviews.Update(existing);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        [Authorize]
        public ActionResult Delete(int id)
        {
            var interview = _uow.Interviews.Get(id);
            if (interview == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && interview.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return View(interview);
        }

        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var interview = _uow.Interviews.Get(id);
            if (interview == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && interview.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            _uow.Interviews.Remove(interview);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        private void LoadLookups(Interview model = null)
        {
            var appsQuery = _uow.Applications.GetAll(a => a.Applicant, a => a.Position).AsQueryable();
            appsQuery = _tenantService.ApplyTenantFilter(appsQuery);

            var usersQuery = _uow.Users.GetAll().AsQueryable();
            usersQuery = _tenantService.ApplyTenantFilter(usersQuery);

            ViewBag.ApplicationId = new SelectList(appsQuery.ToList(), "Id", "Id", model != null ? (object)model.ApplicationId : null);
            ViewBag.InterviewerId = new SelectList(usersQuery.ToList(), "Id", "UserName", model != null ? (object)model.InterviewerId : null);
        }
    }
}






