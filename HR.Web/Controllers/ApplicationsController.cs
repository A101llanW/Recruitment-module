using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;

namespace HR.Web.Controllers
{
    public partial class ApplicationsController : Controller
{
    private readonly UnitOfWork _uow = new UnitOfWork();
    private readonly IStorageService _storage = new StorageService();
    private readonly IEmailService _email = new EmailService();
    private readonly ICandidateEvaluationService _evaluationService = new CandidateEvaluationService();
    private readonly ScoringService _scoringService = new ScoringService();
    private readonly TenantService _tenantService = new TenantService();

    [Authorize]
    public ActionResult TestQuestionnaire()
    {
        var positionId = 4; // Software Developer
        var position = _uow.Positions.GetAll(p => p.PositionQuestions.Select(pq => pq.Question).Select(q => q.QuestionOptions))
            .FirstOrDefault(p => p.Id == positionId);
        
        if (position == null)
            return HttpNotFound();
        
        // Get position questions
        var positionQuestions = _uow.Context.Set<PositionQuestion>()
            .Where(pq => pq.PositionId == positionId)
            .Include(pq => pq.Question)
            .Include(pq => pq.Question.QuestionOptions)
            .OrderBy(pq => pq.Order)
            .ToList();

        ViewBag.Position = position;
        ViewBag.PositionQuestions = positionQuestions;
        ViewBag.Applicant = null; // Will be set in POST
        
        return View();
    }
    
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public ActionResult TestQuestionnaire(int positionId, FormCollection form)
    {
        // Get position questions
        var positionQuestions = _uow.Context.Set<PositionQuestion>()
            .Where(pq => pq.PositionId == positionId)
            .Include(pq => pq.Question)
            .Include(pq => pq.Question.QuestionOptions)
            .OrderBy(pq => pq.Order)
            .ToList();

        var answers = new List<QuestionAnswerViewModel>();
        
        // Process dynamic question answers
        foreach (var pq in positionQuestions)
        {
            var questionFieldName = "question_" + pq.Question.Id;
            var answer = form[questionFieldName];
            
            answers.Add(new QuestionAnswerViewModel
            {
                QuestionId = pq.Question.Id,
                QuestionText = pq.Question.Text,
                QuestionType = pq.Question.Type,
                Answer = answer ?? ""
            });
        }
        
        // Store in session
        Session["TestAnswers"] = answers;
        
        ViewBag.Position = _uow.Positions.Get(positionId);
        ViewBag.PositionQuestions = positionQuestions;
        ViewBag.Answers = answers;
        
        return View();
    }

    // Questionnaire for position application
    public ActionResult Questionnaire(int positionId)
    {
        if (!IsCurrentUserAuthenticated())
        {
            return RedirectToApplicationRegistration();
        }

        var position = GetPositionWithQuestions(positionId);
        if (position == null)
        {
            return HttpNotFound();
        }

        var closedPositionRedirect = GetClosedPositionRedirect(position);
        if (closedPositionRedirect != null)
        {
            return closedPositionRedirect;
        }

        LogPositionQuestions(position);

        ViewBag.Position = position;
        PopulateApplicantViewBag(position.CompanyId);
        ViewBag.PositionQuestions = position.PositionQuestions
            .OrderBy(pq => pq.Order)
            .ToList();

        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public ActionResult Questionnaire(int positionId, FormCollection form, HttpPostedFileBase resume)
    {
        if (positionId <= 0)
        {
            return RedirectToAction("Index", "Positions");
        }

        var position = _uow.Positions.Get(positionId);
        if (position == null)
        {
            return HttpNotFound();
        }

        var tenantValidationResult = ValidatePositionTenantAccess(position, "Access Denied: Position belongs to another company.");
        if (tenantValidationResult != null)
        {
            return tenantValidationResult;
        }

        var positionQuestions = GetPositionQuestions(positionId, true);
        var review = BuildQuestionnaireReviewModel(position, positionQuestions, form);

        string resumePath;
        string resumeError;
        if (!TrySaveResumeForQuestionnaire(resume, out resumePath, out resumeError))
        {
            TempData["ErrorMessage"] = resumeError;
            return RedirectToAction("Questionnaire", new { positionId = positionId });
        }

        StoreQuestionnaireSession(positionId, review.QuestionAnswers, resumePath);
        review.ResumePath = resumePath;

        return View("QuestionnaireReview", review);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public ActionResult FinishQuestionnaire(ApplicationReviewViewModel model, FormCollection form)
    {
        if (model == null || model.PositionId <= 0)
        {
            return RedirectToAction("Index", "Positions");
        }

        var position = _uow.Positions.Get(model.PositionId);
        if (position == null)
        {
            TempData["ErrorMessage"] = "Position not found.";
            return RedirectToAction("Index", "Positions");
        }

        var submissionAccessResult = ValidateQuestionnaireSubmissionAccess(position);
        if (submissionAccessResult != null)
        {
            return submissionAccessResult;
        }

        var applicant = FindOrCreateApplicantForPosition(position.CompanyId);
        if (applicant != null)
        {
            if (HasExistingApplication(applicant.Id, model.PositionId))
            {
                TempData["ErrorMessage"] = "You have already applied for this position.";
                return RedirectToAction("Index", "Positions");
            }

            var application = CreateApplicationFromQuestionnaire(model, position, applicant.Id);
            var questionAnswers = ResolveQuestionnaireAnswers(model.PositionId, form);
            SaveApplicationAnswers(application.Id, questionAnswers);
            ScoreQuestionnaireApplication(application);
        }

        ClearQuestionnaireSession();
        TempData["QuestionnaireSuccess"] = "Your application and questionnaire have been submitted.";
        return RedirectToAction("Index", "Positions");
    }

        public ActionResult Index()
        {
            if (!IsCurrentUserAuthenticated())
            {
                ViewBag.Message = "Please sign in or create account first to view your applications.";
                return View("GuestAccess");
            }

            var user = GetCurrentUser();
            if (user == null)
            {
                return View(Enumerable.Empty<Application>());
            }

            if (IsManagementUser(user))
            {
                return View(BuildManagementApplicationsView());
            }

            return View(GetApplicantApplications(user));
        }

        [Authorize]
        public ActionResult Details(int id)
        {
            var app = _uow.Applications.GetAll(a => a.Applicant, a => a.Position)
                .FirstOrDefault(a => a.Id == id);
            if (app == null)
            {
                return HttpNotFound();
            }

            var user = GetCurrentUser();
            if (user == null)
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var accessCheck = ValidateDetailsAccess(user, app);
            if (accessCheck != null)
            {
                return accessCheck;
            }

            return View(app);
        }

        public ActionResult Create(int? positionId)
        {
            // Check if user is authenticated
            if (User == null || !User.Identity.IsAuthenticated)
            {
                // Store the position they want to apply for
                TempData["ReturnUrl"] = Request.Url != null ? Request.Url.ToString() : null;
                TempData["ApplicationMessage"] = "Please register or login to apply for this position.";
                return RedirectToAction("Register", "Account");
            }

            // If the user is authenticated and not Admin/HR, attempt to preselect their Applicant record
            if (!User.IsInRole("Admin"))
            {
                var username = User.Identity.Name;
                var lowerUsername = username.ToLower();
                var user = _uow.Users.GetAll().FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
                if (user != null)
                {
                    var applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email);
                    if (applicant != null)
                    {
                        ViewBag.CurrentApplicantId = applicant.Id;
                        ViewBag.CurrentApplicantName = applicant.FullName;
                    }
                }
            }
            if (positionId.HasValue)
            {
                var model = new Application { Status = "Interviewing", AppliedOn = DateTime.UtcNow, PositionId = positionId.Value };
                LoadLookups(model);
                return View(model);
            }
            LoadLookups();
            return View(new Application { Status = "Interviewing", AppliedOn = DateTime.UtcNow });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Application model, HttpPostedFileBase resume)
        {
            if (resume != null)
            {
                model.ResumePath = _storage.SaveResume(resume);
            }

            var ownershipError = ValidateApplicationOwnership(model);
            if (!string.IsNullOrWhiteSpace(ownershipError))
            {
                ModelState.AddModelError("", ownershipError);
            }

            if (!ModelState.IsValid)
            {
                LoadLookups(model);
                return View(model);
            }

            model.AppliedOn = DateTime.UtcNow;

            if (!TryAssignAndValidateApplicationCompany(model))
            {
                ModelState.AddModelError("", "You cannot apply for a position in another company.");
                LoadLookups(model);
                return View(model);
            }

            _uow.Applications.Add(model);
            _uow.Complete();
            var applicantEmail = model != null && model.Applicant != null ? model.Applicant.Email : null;
            _email.SendAsync(applicantEmail, "Application received", "We received your application.");
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Edit(int id)
        {
            var app = _uow.Applications.Get(id);
            if (app == null)
            {
                return HttpNotFound();
            }

            // Check if application belongs to user's company
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && app.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            LoadLookups(app);
            return View(app);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Edit(Application model, HttpPostedFileBase resume)
        {
            if (resume != null)
            {
                model.ResumePath = _storage.SaveResume(resume);
            }

            // Check if application belongs to user's company (security check before update)
            var existingApp = _uow.Applications.Get(model.Id);
            if (existingApp == null) return HttpNotFound();
            
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && existingApp.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }
            
            // Sustain CompanyId (prevent tampering)
            model.CompanyId = existingApp.CompanyId;

            if (!ModelState.IsValid)
            {
                LoadLookups(model);
                return View(model);
            }

            _uow.Applications.Update(model);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult PutOnHold(int applicationId)
        {
            var app = _uow.Applications.Get(applicationId);
            if (app == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && app.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }
            app.Status = "On Hold";
            _uow.Applications.Update(app);
            _uow.Complete();
            TempData["Message"] = "Applicant has been put on hold.";
            return RedirectToAction("Index", "Applicants");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult ReleaseHold(int applicationId)
        {
            var app = _uow.Applications.Get(applicationId);
            if (app == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && app.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            // Restore to a status the user can proceed from. Let's use 'Interviewing'.
            app.Status = "Interviewing";
            _uow.Applications.Update(app);
            _uow.Complete();
            TempData["Message"] = "Applicant has been released from hold.";
            return RedirectToAction("Index", "Applicants");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult UpdateStatus(int id, string status)
        {
            var app = _uow.Applications.Get(id);
            if (app == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && app.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }
            
            app.Status = status;
            _uow.Applications.Update(app);
            _uow.Complete();
            return RedirectToAction("Details", new { id });
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Delete(int id)
        {
            var app = _uow.Applications.Get(id);
            if (app == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && app.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return View(app);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult DeleteConfirmed(int id)
        {
            var app = _uow.Applications.Get(id);
            if (app == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && app.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            _uow.Applications.Remove(app);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        private void LoadLookups(Application model = null)
        {
            var applicantsQuery = _uow.Applicants.GetAll().AsQueryable();
            applicantsQuery = _tenantService.ApplyTenantFilter(applicantsQuery);

            var positionsQuery = _uow.Positions.GetAll().AsQueryable();
            positionsQuery = _tenantService.ApplyTenantFilter(positionsQuery);

            ViewBag.ApplicantId = new SelectList(applicantsQuery.ToList(), "Id", "FullName", model != null ? (object)model.ApplicantId : null);
            ViewBag.PositionId = new SelectList(positionsQuery.ToList(), "Id", "Title", model != null ? (object)model.PositionId : null);
        }
    }
}
