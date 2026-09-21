using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;
using System.Data.Entity;

namespace HR.Web.Controllers
{
    [ModuleAccess(RoleModuleCatalog.Interviews)]
    public partial class InterviewsController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly IEmailService _email = new EmailService();
        private readonly IEmailTemplateService _emailTemplateService = new EmailTemplateService();
        private readonly AuditService _auditService = new AuditService();
        private readonly TenantService _tenantService = new TenantService();

        private string GetInterviewActorName()
        {
            return User?.Identity?.Name ?? "System";
        }

        public ActionResult Index()
        {
            var rolePermissionService = new RolePermissionService();
            ViewBag.CanManageInterviews = false;
            PopulatePendingInterviewEmailContext();

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
                ViewBag.CanManageInterviews = rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Interviews, RoleAccessLevels.Manage);
                var interviews = GetManagementInterviews()
                    .OrderByDescending(i => i.ScheduledAt)
                    .ToList();

                if ((bool)ViewBag.CanManageInterviews)
                {
                    ViewBag.ApplicationsWithoutScheduledInterview = GetApplicationsWithoutScheduledInterview(interviews);
                    PopulateInterviewEmailCcForIndexView();
                }

                return View(interviews);
            }

            return View(GetApplicantInterviews(user)
                .OrderByDescending(i => i.ScheduledAt)
                .ToList());
        }

        private User GetCurrentInterviewUser()
        {
            if (User?.Identity == null || string.IsNullOrEmpty(User.Identity.Name))
            {
                return null;
            }

            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            return _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
        }

        private bool IsManagementUser(User user)
        {
            if (user == null)
            {
                return User != null && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"));
            }

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
        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult BookInterview(int applicationId, int interviewerId, DateTime scheduledAt, string mode, string returnTo = null, int? resumeEmailApplicationId = null)
        {
            try
            {
                var application = _uow.Applications.Get(applicationId);
                if (application == null) return HttpNotFound();

                var accessDenied = ValidateInterviewApplicationAccess(application);
                if (accessDenied != null)
                {
                    return accessDenied;
                }

                var interview = CreateScheduledInterview(applicationId, interviewerId, scheduledAt, mode);
                NotifyInterviewerOfBooking(interviewerId, interview.Id, applicationId, scheduledAt, mode);
                return GetBookInterviewSuccessRedirect(returnTo, resumeEmailApplicationId, applicationId);
            }
            catch (Exception ex)
            {
                _auditService.LogAction(GetInterviewActorName(), "CREATE", "Interviews", "new", 
                    wasSuccessful: false, errorMessage: ex.Message);

                TempData["Error"] = "Error booking interview: " + ex.Message;
                return GetBookInterviewErrorRedirect(returnTo, resumeEmailApplicationId, applicationId);
            }
        }

        private Interview CreateScheduledInterview(int applicationId, int interviewerId, DateTime scheduledAt, string mode)
        {
            var companyId = _tenantService.GetCurrentUserCompanyId();
            var interview = new Interview
            {
                ApplicationId = applicationId,
                InterviewerId = interviewerId,
                ScheduledAt = scheduledAt,
                Mode = mode,
                CompanyId = companyId
            };
            _uow.Interviews.Add(interview);
            _uow.Complete();

            _auditService.LogCreate(GetInterviewActorName(), "Interviews", interview.Id.ToString(), new
            {
                ApplicationId = applicationId,
                InterviewerId = interviewerId,
                ScheduledAt = scheduledAt,
                Mode = mode
            });

            return interview;
        }

        private ActionResult GetBookInterviewSuccessRedirect(string returnTo, int? resumeEmailApplicationId, int applicationId)
        {
            if (!string.Equals(returnTo, "interviews", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index");
            }

            var applicationToResume = resumeEmailApplicationId.HasValue && resumeEmailApplicationId.Value > 0
                ? resumeEmailApplicationId.Value
                : applicationId;
            TempData["InterviewEmailInfo"] = "Interview booked. You can now proceed with candidate email.";
            return RedirectToAction("Index", new { resumeEmailApplicationId = applicationToResume });
        }

        private ActionResult GetBookInterviewErrorRedirect(string returnTo, int? resumeEmailApplicationId, int applicationId)
        {
            if (!string.Equals(returnTo, "interviews", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index");
            }

            return RedirectToAction("Index", new
            {
                resumeEmailApplicationId = resumeEmailApplicationId.HasValue && resumeEmailApplicationId.Value > 0
                    ? resumeEmailApplicationId.Value
                    : applicationId
            });
        }

        private ActionResult ValidateInterviewApplicationAccess(Application application)
        {
            return ValidateInterviewTenantAccess(application.CompanyId);
        }

        private ActionResult ValidateInterviewTenantAccess(int? interviewCompanyId)
        {
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && interviewCompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return null;
        }

        private Application LoadInterviewApplication(int applicationId)
        {
            return _uow.Context.Applications
                .Include("Applicant")
                .Include("Position")
                .FirstOrDefault(a => a.Id == applicationId);
        }

        private Interview GetLatestInterviewForApplication(int applicationId)
        {
            return _uow.Context.Interviews
                .Where(i => i.ApplicationId == applicationId)
                .OrderByDescending(i => i.ScheduledAt)
                .FirstOrDefault();
        }

        internal ActionResult RedirectForMissingInterview(Application application, int applicationId, string draftBody)
        {
            if (!string.IsNullOrWhiteSpace(draftBody))
            {
                Session[GetPendingInterviewEmailSessionKey(applicationId)] = draftBody;
            }

            TempData["InterviewEmailError"] = "This candidate has no interview scheduled yet.";
            TempData["InterviewEmailSchedulePromptApplicationId"] = applicationId;
            TempData["InterviewEmailSchedulePromptCandidateName"] = application.Applicant != null
                ? application.Applicant.FullName
                : "Candidate";
            return RedirectToAction("Index");
        }

        [TenantAuthorize]
        public ActionResult Details(int id)
        {
            var interview = _uow.Interviews.GetAll(i => i.Application.Applicant, i => i.Application.Position, i => i.Interviewer)
                .FirstOrDefault(i => i.Id == id);
            if (interview == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var accessDenied = ValidateInterviewTenantAccess(interview.CompanyId);
            if (accessDenied != null)
            {
                return accessDenied;
            }

            return View(interview);
        }

        [TenantAuthorize]
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

        [TenantAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Interview model)
        {
            if (model == null)
            {
                LoadLookups();
                return View(new Interview { ScheduledAt = DateTime.UtcNow.AddDays(1) });
            }

            var interviewModel = model;
            if (!ModelState.IsValid)
            {
                LoadLookups(interviewModel);
                return View(interviewModel);
            }
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue)
            {
                interviewModel.CompanyId = companyId.Value;
            }

            _uow.Interviews.Add(interviewModel);
            _uow.Complete();
            TryNotifyInterviewerAfterInterviewCreated(interviewModel);
            return RedirectToAction("Index");
        }

        [TenantAuthorize]
        public ActionResult Edit(int id)
        {
            var interview = _uow.Interviews.GetAll(
                    i => i.Application.Applicant,
                    i => i.Application.Position,
                    i => i.Interviewer)
                .FirstOrDefault(i => i.Id == id);
            if (interview == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var accessDenied = ValidateInterviewTenantAccess(interview.CompanyId);
            if (accessDenied != null)
            {
                return accessDenied;
            }

            LoadLookups(interview);
            return View(interview);
        }

        [TenantAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Interview model)
        {
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            var interviewModel = model;
            if (!ModelState.IsValid)
            {
                LoadLookups(interviewModel);
                return View(interviewModel);
            }

            var existing = _uow.Interviews.GetAll(i => i.Application).FirstOrDefault(i => i.Id == interviewModel.Id);
            if (existing == null) return HttpNotFound();

            var accessDenied = ValidateInterviewTenantAccess(existing.CompanyId);
            if (accessDenied != null)
            {
                return accessDenied;
            }
            
            existing.ScheduledAt = interviewModel.ScheduledAt;
            existing.Mode = interviewModel.Mode;
            existing.Notes = interviewModel.Notes;
            existing.InterviewerId = interviewModel.InterviewerId;

            _uow.Interviews.Update(existing);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        [TenantAuthorize]
        public ActionResult Delete(int id)
        {
            var interview = _uow.Interviews.GetAll(
                    i => i.Application.Applicant,
                    i => i.Application.Position)
                .FirstOrDefault(i => i.Id == id);
            if (interview == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var accessDenied = ValidateInterviewTenantAccess(interview.CompanyId);
            if (accessDenied != null)
            {
                return accessDenied;
            }

            return View(interview);
        }

        [TenantAuthorize]
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
            var accessDenied = ValidateInterviewTenantAccess(interview.CompanyId);
            if (accessDenied != null)
            {
                return accessDenied;
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

        private List<Application> GetApplicationsWithoutScheduledInterview(IEnumerable<Interview> interviews)
        {
            var scheduledApplicationIds = new HashSet<int>(
                interviews != null
                    ? interviews.Select(i => i.ApplicationId)
                    : Enumerable.Empty<int>());

            var appsQuery = _uow.Context.Applications
                .Include("Applicant")
                .Include("Position")
                .AsQueryable();
            appsQuery = _tenantService.ApplyTenantFilter(appsQuery);

            return appsQuery
                .ToList()
                .Where(a => !scheduledApplicationIds.Contains(a.Id))
                .OrderByDescending(a => a.AppliedOn)
                .ToList();
        }

        private void PopulatePendingInterviewEmailContext()
        {
            int resumeEmailApplicationId;
            if (!int.TryParse(Request.QueryString["resumeEmailApplicationId"], out resumeEmailApplicationId) || resumeEmailApplicationId <= 0)
            {
                return;
            }

            ViewBag.ResumeEmailApplicationId = resumeEmailApplicationId;
            ViewBag.ResumeEmailMessage = Session[GetPendingInterviewEmailSessionKey(resumeEmailApplicationId)] as string;
        }

        internal static string GetPendingInterviewEmailSessionKey(int applicationId)
        {
            return string.Format("PendingInterviewEmailMessage_{0}", applicationId);
        }
    }
}






