using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using HR.Web.Data;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    [ModuleAccess(RoleModuleCatalog.Applications)]
    public partial class ApplicationsController : Controller
{
    private readonly UnitOfWork _uow = new UnitOfWork();
    private readonly IStorageService _storage = new StorageService();
    private readonly IEmailService _email = new EmailService();
    private readonly IEmailTemplateService _emailTemplateService = new EmailTemplateService();
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
            if (pq?.Question == null)
            {
                continue;
            }

            var questionFieldName = "question_" + pq.Question.Id;
            var answer = form != null ? form[questionFieldName] : null;
            
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

        var positionForCompany = _uow.Positions.Get(positionId);
        if (positionForCompany == null)
        {
            return HttpNotFound();
        }

        var applicantResult = RequireApplicantForPosition(positionForCompany.CompanyId, out var applicant);
        if (applicantResult != null)
        {
            return applicantResult;
        }

        var workflowResult = TryValidateQuestionnaireWorkflow(positionId, applicant, out var position, out var activeQuestionnaireStage, out _);
        if (workflowResult != null)
        {
            return workflowResult;
        }

        var profileResult = RequireCompleteApplicantProfile(applicant, position);
        if (profileResult != null)
        {
            return profileResult;
        }

        LogPositionQuestions(position);

        ViewBag.Position = position;
        PopulateApplicantViewBag(position.CompanyId);
        ViewBag.PositionQuestions = position.PositionQuestions
            .Where(pq => pq.StageNumber == activeQuestionnaireStage)
            .OrderBy(pq => pq.Order)
            .ToList();

        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public ActionResult Questionnaire(int positionId, FormCollection form, HttpPostedFileBase resume, bool acceptLegalTerms = false)
    {
        if (positionId <= 0)
        {
            return RedirectToAction("Index", "Positions");
        }

        if (form == null)
        {
            TempData["ErrorMessage"] = "Questionnaire submission was incomplete. Please try again.";
            return RedirectToAction("Questionnaire", new { positionId = positionId });
        }

        var submittedForm = form;
        var position = _uow.Positions.Get(positionId);
        if (position == null)
        {
            return HttpNotFound();
        }

        var accessResult = ValidateQuestionnaireApplicantAccess(position, out var applicant);
        if (accessResult != null)
        {
            return accessResult;
        }

        var workflowResult = TryValidateQuestionnaireWorkflow(positionId, applicant, out var positionWithQuestions, out var activeQuestionnaireStage, out _);
        if (workflowResult != null)
        {
            return workflowResult;
        }

        if (!acceptLegalTerms)
        {
            TempData["ErrorMessage"] = "You must agree to the candidate Terms & Conditions and Privacy Policy to continue.";
            return RedirectToAction("Questionnaire", new { positionId = positionId });
        }

        var positionQuestions = GetPositionQuestions(positionId, true, activeQuestionnaireStage);
        var review = BuildQuestionnaireReviewModel(positionWithQuestions, positionQuestions, submittedForm);

        string resumePath;
        string resumeError;
        if (!TrySaveResumeForQuestionnaire(resume, out resumePath, out resumeError))
        {
            TempData["ErrorMessage"] = resumeError;
            return RedirectToAction("Questionnaire", new { positionId = positionId });
        }

        review.AcceptLegalTerms = true;
        StoreQuestionnaireSession(positionId, review.QuestionAnswers, resumePath, activeQuestionnaireStage, true);
        review.ResumePath = resumePath;

        return View("QuestionnaireReview", review);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public ActionResult FinishQuestionnaire(ApplicationReviewViewModel model, FormCollection form, bool acceptLegalTerms = false)
    {
        if (model == null || model.PositionId <= 0)
        {
            return RedirectToAction("Index", "Positions");
        }

        var reviewModel = model;
        var position = _uow.Positions.Get(reviewModel.PositionId);
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

        var sessionStage = ResolveQuestionnaireSessionStage();
        var maxStages = Math.Max(1, position.QuestionnaireStageCount);

        var applicantResult = RequireApplicantForPosition(position.CompanyId, out var applicant);
        if (applicantResult != null)
        {
            return applicantResult;
        }

        if (!acceptLegalTerms)
        {
            TempData["ErrorMessage"] = "You must agree to the candidate Terms & Conditions and Privacy Policy to finish your application.";
            return RedirectToAction("Questionnaire", new { positionId = reviewModel.PositionId });
        }

        LegalPolicyHelper.ApplyApplicantAcceptance(applicant, DateTime.UtcNow);
        _uow.Applicants.Update(applicant);
        _uow.Complete();

        var existingApplication = _uow.Applications.GetAll()
            .FirstOrDefault(a => a.ApplicantId == applicant.Id && a.PositionId == reviewModel.PositionId);

        if (existingApplication == null)
        {
            if (sessionStage != 1)
            {
                TempData["ErrorMessage"] = "Your questionnaire session is invalid. Please start again from the questionnaire.";
                return RedirectToAction("Questionnaire", new { positionId = reviewModel.PositionId });
            }

            var initialSubmitResult = SubmitInitialQuestionnaireApplication(reviewModel, position, applicant, form);
            if (initialSubmitResult != null)
            {
                return initialSubmitResult;
            }
        }
        else
        {
            var pendingSubmitResult = SubmitPendingQuestionnaireStage(existingApplication, reviewModel, sessionStage, form);
            if (pendingSubmitResult != null)
            {
                return pendingSubmitResult;
            }
        }

        ClearQuestionnaireSession();
        TempData["QuestionnaireSuccess"] = maxStages > 1
            ? "Your questionnaire responses have been submitted."
            : "Your application and questionnaire have been submitted.";
        return RedirectToAction("Index", "Positions");
    }

    [Authorize]
    public ActionResult ProfileDetails(int positionId)
    {
        if (!IsCurrentUserAuthenticated())
        {
            return RedirectToApplicationRegistration();
        }

        var position = _uow.Positions.Get(positionId);
        if (position == null)
        {
            return HttpNotFound();
        }

        var closedPositionRedirect = GetClosedPositionRedirect(position);
        if (closedPositionRedirect != null)
        {
            return closedPositionRedirect;
        }

        var applicant = FindOrCreateApplicantForPosition(position.CompanyId);
        if (applicant == null)
        {
            TempData["ErrorMessage"] = "Please complete your applicant profile before continuing.";
            return RedirectToAction("Index", "Positions");
        }

        var profile = GetApplicantProfile(applicant.Id);
        var model = BuildApplicantProfileViewModel(position, applicant, profile);

        ApplyPendingLinkedInImport(model);
        PopulateLinkedInImportViewBag();
        return View("ProfileDetails", model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public ActionResult ProfileDetails(ApplicantProfileViewModel model)
    {
        if (model == null)
        {
            return RedirectToAction("Index", "Positions");
        }

        var profileModel = model;
        var position = _uow.Positions.Get(profileModel.PositionId);
        if (position == null)
        {
            return HttpNotFound();
        }

        var applicant = FindOrCreateApplicantForPosition(position.CompanyId);
        if (applicant == null)
        {
            TempData["ErrorMessage"] = "Please complete your applicant profile before continuing.";
            return RedirectToAction("Index", "Positions");
        }

        profileModel.PositionTitle = position.Title;
        profileModel.IsTechnical = position.IsTechnical == true;
        profileModel.ApplicantId = applicant.Id;
        NormalizeSelectableProfileFields(profileModel);
        ValidateTechnicalProfileFields(profileModel);

        if (!ModelState.IsValid)
        {
            PopulateLinkedInImportViewBag();
            return View("ProfileDetails", profileModel);
        }

        var profile = GetApplicantProfile(applicant.Id);
        if (profile == null)
        {
            profile = new ApplicantProfile
            {
                ApplicantId = applicant.Id,
                CreatedOn = DateTime.UtcNow
            };
            _uow.Context.Set<ApplicantProfile>().Add(profile);
        }

        ApplyApplicantProfileViewModel(applicant, profile, profileModel);

        _uow.Complete();
        ClearPendingLinkedInImport();

        TempData["SuccessMessage"] = "Profile details saved. Continue to the questionnaire.";
        return RedirectToAction("Questionnaire", new { positionId = position.Id });
    }

        public ActionResult Index()
        {
            var rolePermissionService = new RolePermissionService();
            ViewBag.CanManageApplications = false;
            ViewBag.CanInviteQuestionnaireSecondaryStage = false;

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
                PopulateFailedCandidateEmailApplicationIdsForIndexView();
                ViewBag.CanManageApplications = rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Applications, RoleAccessLevels.Manage);
                ViewBag.CanInviteQuestionnaireSecondaryStage = rolePermissionService.IsFullCompanyAdmin(user) ||
                    _tenantService.IsActualSuperAdmin();
                return View(BuildManagementApplicationsView());
            }

            ViewBag.CanInviteQuestionnaireSecondaryStage = false;
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

            PopulateApplicationDetailsViewBag(user, app);

            return View(app);
        }

        public ActionResult Create(int? positionId)
        {
            if (!IsCurrentUserAuthenticated())
            {
                return RedirectToApplicationRegistration();
            }

            // If the user is authenticated and not Admin/HR, attempt to preselect their Applicant record
            if (!User.IsInRole("Admin"))
            {
                if (!IsCurrentUserAuthenticated())
                {
                    LoadLookups();
                    return View(new Application { Status = "Interviewing", AppliedOn = DateTime.UtcNow });
                }

                var username = GetApplicationsActorName();
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
            if (model == null)
            {
                LoadLookups();
                return View(new Application { Status = "Interviewing", AppliedOn = DateTime.UtcNow });
            }

            var applicationModel = model;
            if (resume != null)
            {
                applicationModel.ResumePath = _storage.SaveResume(resume);
            }

            var ownershipError = ValidateApplicationOwnership(applicationModel);
            if (!string.IsNullOrWhiteSpace(ownershipError))
            {
                ModelState.AddModelError("", ownershipError);
            }

            if (!ModelState.IsValid)
            {
                LoadLookups(applicationModel);
                return View(applicationModel);
            }

            applicationModel.AppliedOn = DateTime.UtcNow;

            if (!TryAssignAndValidateApplicationCompany(applicationModel))
            {
                ModelState.AddModelError("", "You cannot apply for a position in another company.");
                LoadLookups(applicationModel);
                return View(applicationModel);
            }

            _uow.Applications.Add(applicationModel);
            _uow.Complete();
            var applicantEmail = applicationModel.Applicant != null ? applicationModel.Applicant.Email : null;
            _email.SendAsync(applicantEmail, "Application received", "We received your application.");
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Edit(int id)
        {
            var accessResult = TryGetManagedApplication(id, out var app);
            if (accessResult != null)
            {
                return accessResult;
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
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            var applicationModel = model;
            if (resume != null)
            {
                applicationModel.ResumePath = _storage.SaveResume(resume);
            }

            var accessResult = TryGetManagedApplication(applicationModel.Id, out var existingApp);
            if (accessResult != null)
            {
                return accessResult;
            }
            
            applicationModel.CompanyId = existingApp.CompanyId;

            if (!ModelState.IsValid)
            {
                LoadLookups(applicationModel);
                return View(applicationModel);
            }

            _uow.Applications.Update(applicationModel);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult UpdatePositionPassMark(int positionId, decimal passMark)
        {
            if (positionId <= 0)
            {
                return Json(new { success = false, message = "Invalid position selected." });
            }

            var position = _uow.Positions.Get(positionId);
            if (position == null)
            {
                return Json(new { success = false, message = "Position not found." });
            }

            var tenantValidationResult = ValidatePositionTenantAccess(position, "Access Denied");
            if (tenantValidationResult != null)
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var normalizedPassMark = Math.Max(0m, Math.Min(100m, Math.Round(passMark, 0, MidpointRounding.AwayFromZero)));
            position.PassMark = normalizedPassMark;
            _uow.Positions.Update(position);
            _uow.Complete();

            return Json(new
            {
                success = true,
                passMark = normalizedPassMark
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult UpdateStatus(int id, string status)
        {
            var accessResult = TryGetManagedApplication(id, out var app);
            if (accessResult != null)
            {
                return accessResult;
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
            var accessResult = TryGetManagedApplication(id, out var app);
            if (accessResult != null)
            {
                return accessResult;
            }

            return View(app);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult DeleteConfirmed(int id)
        {
            var accessResult = TryGetManagedApplication(id, out var app);
            if (accessResult != null)
            {
                return accessResult;
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
