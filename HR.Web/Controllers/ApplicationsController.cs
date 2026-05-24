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

        var sessionStageObj = Session["QuestionnaireActiveStage"] as int?;
        if (!sessionStageObj.HasValue)
        {
            var rawStage = Session["QuestionnaireActiveStage"] as string;
            int parsedStage;
            if (!string.IsNullOrEmpty(rawStage) && int.TryParse(rawStage, out parsedStage))
            {
                sessionStageObj = parsedStage;
            }
        }

        var sessionStage = sessionStageObj ?? 1;
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

            if (HasExistingApplication(applicant.Id, reviewModel.PositionId))
            {
                TempData["ErrorMessage"] = "You have already applied for this position.";
                return RedirectToAction("Index", "Positions");
            }

            var application = CreateApplicationFromQuestionnaire(reviewModel, position, applicant.Id);
            var questionAnswers = ResolveQuestionnaireAnswers(reviewModel.PositionId, form);
            SaveApplicationAnswers(application.Id, questionAnswers, 1);
            application.LastCompletedQuestionnaireStage = 1;
            application.PendingQuestionnaireStage = null;
            _uow.Applications.Update(application);
            _uow.Complete();
            ScoreQuestionnaireApplication(application);
        }
        else
        {
            if (!existingApplication.PendingQuestionnaireStage.HasValue ||
                existingApplication.PendingQuestionnaireStage.Value != sessionStage)
            {
                TempData["ErrorMessage"] = "You cannot submit the questionnaire using this link right now. If you were sent a new link by email, open that link instead.";
                return RedirectToAction("Index", "Positions");
            }

            var questionAnswers = ResolveQuestionnaireAnswers(reviewModel.PositionId, form);
            SaveApplicationAnswers(existingApplication.Id, questionAnswers, sessionStage);
            if (existingApplication.Score.HasValue)
            {
                existingApplication.LastQuestionnaireScore = existingApplication.Score;
            }

            existingApplication.LastCompletedQuestionnaireStage = sessionStage;
            existingApplication.PendingQuestionnaireStage = null;
            _uow.Applications.Update(existingApplication);
            _uow.Complete();
            ScoreQuestionnaireApplication(existingApplication);
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
        var model = new ApplicantProfileViewModel
        {
            PositionId = position.Id,
            PositionTitle = position.Title,
            ApplicantId = applicant.Id,
            IsTechnical = position.IsTechnical == true,
            FullName = applicant.FullName,
            Email = applicant.Email,
            Phone = applicant.Phone,
            Location = profile != null ? profile.Location : null,
            TotalYearsExperience = profile != null ? profile.TotalYearsExperience : null,
            RelevantYearsExperience = profile != null ? profile.RelevantYearsExperience : null,
            MostRecentCompany = profile != null ? profile.MostRecentCompany : null,
            MostRecentTitle = profile != null ? profile.MostRecentTitle : null,
            MostRecentStartDate = profile != null ? profile.MostRecentStartDate : null,
            MostRecentEndDate = profile != null ? profile.MostRecentEndDate : null,
            SecondMostRecentCompany = profile != null ? profile.SecondMostRecentCompany : null,
            SecondMostRecentTitle = profile != null ? profile.SecondMostRecentTitle : null,
            SecondMostRecentStartDate = profile != null ? profile.SecondMostRecentStartDate : null,
            SecondMostRecentEndDate = profile != null ? profile.SecondMostRecentEndDate : null,
            EmploymentType = profile != null ? profile.EmploymentType : null,
            Skills = profile != null ? profile.Skills : null,
            Competencies = profile != null ? profile.Competencies : null,
            EducationDegree = profile != null ? profile.EducationDegree : null,
            EducationInstitution = profile != null ? profile.EducationInstitution : null,
            KeyAchievement = profile != null ? profile.KeyAchievement : null,
            Certifications = profile != null ? profile.Certifications : null,
            PortfolioUrl = profile != null ? profile.PortfolioUrl : null,
            WorkAuthorization = profile != null && profile.WorkAuthorization,
            NoticePeriod = profile != null ? profile.NoticePeriod : null
        };

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

        if (profileModel.IsTechnical)
        {
            if (!profileModel.RelevantYearsExperience.HasValue)
            {
                ModelState.AddModelError("RelevantYearsExperience", "Relevant years of experience is required for technical roles.");
            }

            if (string.IsNullOrWhiteSpace(profileModel.Skills))
            {
                ModelState.AddModelError("Skills", "Please list your core technical skills.");
            }
        }
        

        if (!ModelState.IsValid)
        {
            PopulateLinkedInImportViewBag();
            return View("ProfileDetails", profileModel);
        }

        applicant.FullName = profileModel.FullName != null ? profileModel.FullName.Trim() : applicant.FullName;
        applicant.Email = profileModel.Email != null ? profileModel.Email.Trim() : applicant.Email;
        applicant.Phone = profileModel.Phone != null ? profileModel.Phone.Trim() : applicant.Phone;

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

        profile.Location = profileModel.Location != null ? profileModel.Location.Trim() : null;
        profile.TotalYearsExperience = profileModel.TotalYearsExperience;
        profile.RelevantYearsExperience = profileModel.RelevantYearsExperience;
        profile.MostRecentCompany = profileModel.MostRecentCompany != null ? profileModel.MostRecentCompany.Trim() : null;
        profile.MostRecentTitle = profileModel.MostRecentTitle != null ? profileModel.MostRecentTitle.Trim() : null;
        profile.MostRecentStartDate = profileModel.MostRecentStartDate;
        profile.MostRecentEndDate = profileModel.MostRecentEndDate;
        profile.SecondMostRecentCompany = profileModel.SecondMostRecentCompany != null ? profileModel.SecondMostRecentCompany.Trim() : null;
        profile.SecondMostRecentTitle = profileModel.SecondMostRecentTitle != null ? profileModel.SecondMostRecentTitle.Trim() : null;
        profile.SecondMostRecentStartDate = profileModel.SecondMostRecentStartDate;
        profile.SecondMostRecentEndDate = profileModel.SecondMostRecentEndDate;
        profile.EmploymentType = profileModel.EmploymentType != null ? profileModel.EmploymentType.Trim() : null;
        profile.Skills = profileModel.Skills != null ? profileModel.Skills.Trim() : null;
        profile.Competencies = profileModel.Competencies != null ? profileModel.Competencies.Trim() : null;
        profile.EducationDegree = profileModel.EducationDegree != null ? profileModel.EducationDegree.Trim() : null;
        profile.EducationInstitution = profileModel.EducationInstitution != null ? profileModel.EducationInstitution.Trim() : null;
        profile.KeyAchievement = profileModel.KeyAchievement != null ? profileModel.KeyAchievement.Trim() : null;
        profile.Certifications = profileModel.Certifications != null ? profileModel.Certifications.Trim() : null;
        profile.PortfolioUrl = profileModel.PortfolioUrl != null ? profileModel.PortfolioUrl.Trim() : null;
        profile.WorkAuthorization = profileModel.WorkAuthorization;
        profile.NoticePeriod = profileModel.NoticePeriod != null ? profileModel.NoticePeriod.Trim() : null;
        profile.UpdatedOn = DateTime.UtcNow;

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

            var rolePermissionService = new RolePermissionService();
            var canManageApps = rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Applications, RoleAccessLevels.Manage);
            var isMgmt = IsManagementUser(user);
            var isCompanyAdminOrSuper = rolePermissionService.IsFullCompanyAdmin(user) || _tenantService.IsActualSuperAdmin();
            var maxQ = app.Position != null ? Math.Max(1, app.Position.QuestionnaireStageCount) : 1;
            var canInviteNext = isCompanyAdminOrSuper &&
                maxQ > 1 &&
                app.LastCompletedQuestionnaireStage > 0 &&
                app.LastCompletedQuestionnaireStage < maxQ &&
                !app.PendingQuestionnaireStage.HasValue;
            ViewBag.CanOpenNextQuestionnaireStage = canInviteNext;
            ViewBag.QuestionnaireStageCountForDetails = maxQ;
            ViewBag.ShowQuestionnaireHiringPanel = isMgmt && canManageApps && maxQ > 1;

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
            if (resume != null)
            {
                model.ResumePath = _storage.SaveResume(resume);
            }

            var accessResult = TryGetManagedApplication(model.Id, out var existingApp);
            if (accessResult != null)
            {
                return accessResult;
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
