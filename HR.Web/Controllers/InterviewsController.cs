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
    public class InterviewsController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly IEmailService _email = new EmailService();
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
        [Authorize(Roles = "Admin, SuperAdmin")]
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

        private void NotifyInterviewerOfBooking(int interviewerId, int interviewId, int applicationId, DateTime scheduledAt, string mode)
        {
            var interviewer = _uow.Users.Get(interviewerId);
            if (interviewer != null)
            {
                _email.SendAsync(interviewer.Email, "Interview scheduled", "You have a new interview scheduled.");
            }
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public async Task<ActionResult> SendInterviewCandidateEmail(int applicationId, string message)
        {
            var application = LoadInterviewApplication(applicationId);
            if (application == null)
            {
                return HttpNotFound();
            }

            var accessDenied = ValidateInterviewApplicationAccess(application);
            if (accessDenied != null)
            {
                return accessDenied;
            }

            var trimmedMessage = ValidateInterviewEmailMessage(message);
            if (trimmedMessage == null)
            {
                TempData["InterviewEmailError"] = "Please provide a message before sending.";
                return RedirectToAction("Index");
            }

            var interview = GetLatestInterviewForApplication(applicationId);
            if (interview == null)
            {
                return RedirectForMissingInterview(application, applicationId, trimmedMessage);
            }

            var recipientEmail = application.Applicant != null ? application.Applicant.Email : null;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                TempData["InterviewEmailError"] = "Candidate has no email address on file.";
                return RedirectToAction("Index");
            }

            await SendInterviewUpdateEmail(application, interview, recipientEmail, trimmedMessage);
            Session.Remove(GetPendingInterviewEmailSessionKey(applicationId));
            TempData["InterviewEmailSuccess"] = string.Format(
                "Email sent to {0}.",
                application.Applicant != null && !string.IsNullOrWhiteSpace(application.Applicant.FullName)
                    ? application.Applicant.FullName
                    : recipientEmail.Trim());

            return RedirectToAction("Index");
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

        private ActionResult RedirectForMissingInterview(Application application, int applicationId, string trimmedMessage)
        {
            Session[GetPendingInterviewEmailSessionKey(applicationId)] = trimmedMessage;
            TempData["InterviewEmailError"] = "This candidate has no interview scheduled yet.";
            TempData["InterviewEmailSchedulePromptApplicationId"] = applicationId;
            TempData["InterviewEmailSchedulePromptCandidateName"] = application.Applicant != null
                ? application.Applicant.FullName
                : "Candidate";
            return RedirectToAction("Index");
        }

        private async Task SendInterviewUpdateEmail(Application application, Interview interview, string recipientEmail, string trimmedMessage)
        {
            var positionTitle = application.Position != null ? application.Position.Title : "the position";
            var subject = string.Format("Interview update for {0}", positionTitle);
            var body = BuildInterviewCandidateEmailBody(
                application.Applicant != null ? application.Applicant.FullName : null,
                positionTitle,
                interview.ScheduledAt,
                interview.Mode,
                trimmedMessage);

            await _email.SendAsync(recipientEmail.Trim(), subject, body);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public async Task<ActionResult> SendInterviewCandidatesBatchEmail(string message)
        {
            var trimmedMessage = ValidateInterviewEmailMessage(message);
            if (trimmedMessage == null)
            {
                TempData["InterviewEmailError"] = "Please provide a message before sending (max 4000 characters).";
                return RedirectToAction("Index");
            }

            var scheduledInterviews = GetManagementInterviews()
                .OrderByDescending(i => i.ScheduledAt)
                .ToList()
                .GroupBy(i => i.ApplicationId)
                .Select(g => g.First())
                .ToList();

            if (!scheduledInterviews.Any())
            {
                TempData["InterviewEmailError"] = "No scheduled interviews found for batch email.";
                return RedirectToAction("Index");
            }

            var interviewRecipients = scheduledInterviews
                .Where(i => i.Application != null &&
                            i.Application.Applicant != null &&
                            !string.IsNullOrWhiteSpace(i.Application.Applicant.Email))
                .ToList();

            if (!interviewRecipients.Any())
            {
                TempData["InterviewEmailError"] = "No candidate email addresses found for scheduled interviews.";
                return RedirectToAction("Index");
            }

            var sendTasks = interviewRecipients.Select(interview =>
            {
                var applicant = interview.Application.Applicant;
                var positionTitle = interview.Application.Position != null
                    ? interview.Application.Position.Title
                    : "the position";
                var subject = string.Format("Interview update for {0}", positionTitle);
                var body = BuildInterviewCandidateEmailBody(
                    applicant.FullName,
                    positionTitle,
                    interview.ScheduledAt,
                    interview.Mode,
                    trimmedMessage);

                return _email.SendAsync(applicant.Email.Trim(), subject, body);
            });

            await Task.WhenAll(sendTasks);

            TempData["InterviewEmailSuccess"] = string.Format(
                "Batch email sent to {0} candidate{1} with scheduled interviews.",
                interviewRecipients.Count,
                interviewRecipients.Count == 1 ? string.Empty : "s");
            return RedirectToAction("Index");
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
            var accessDenied = ValidateInterviewTenantAccess(interview.CompanyId);
            if (accessDenied != null)
            {
                return accessDenied;
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
            var interviewerEmail = interviewModel.Interviewer != null ? interviewModel.Interviewer.Email : null;
            _email.SendAsync(interviewerEmail, "Interview scheduled", "Please attend.");
            return RedirectToAction("Index");
        }

        [Authorize]
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

        [Authorize]
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

        [Authorize]
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

        private string ValidateInterviewEmailMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            var trimmed = message.Trim();
            if (trimmed.Length > 4000)
            {
                return null;
            }

            return trimmed;
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

        private static string BuildInterviewCandidateEmailBody(string applicantName, string positionTitle, DateTime scheduledAt, string mode, string customMessage)
        {
            var safeApplicantName = HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(applicantName) ? "Candidate" : applicantName.Trim());
            var safePositionTitle = HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(positionTitle) ? "the position" : positionTitle.Trim());
            var safeMode = HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(mode) ? "Interview" : mode.Trim());
            var safeScheduledAt = HttpUtility.HtmlEncode(scheduledAt.ToString("f"));
            var safeMessage = HttpUtility.HtmlEncode(customMessage ?? string.Empty)
                .Replace("\r\n", "<br/>")
                .Replace("\n", "<br/>");

            return string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Interview Update</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <p>Dear {0},</p>
    <p>This is an update regarding your interview for <strong>{1}</strong>.</p>
    <p><strong>Scheduled:</strong> {2}</p>
    <p><strong>Mode:</strong> {3}</p>
    <p>{4}</p>
    <p>Regards,<br/>Recruitment Team</p>
</body>
</html>", safeApplicantName, safePositionTitle, safeScheduledAt, safeMode, safeMessage);
        }

        private static string GetPendingInterviewEmailSessionKey(int applicationId)
        {
            return string.Format("PendingInterviewEmailMessage_{0}", applicationId);
        }
    }
}






