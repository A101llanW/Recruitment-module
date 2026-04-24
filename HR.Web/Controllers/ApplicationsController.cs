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
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    [ModuleAccess(RoleModuleCatalog.Applications)]
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

        var applicant = FindOrCreateApplicantForPosition(position.CompanyId);
        if (applicant == null)
        {
            TempData["ErrorMessage"] = "Please complete your applicant profile before continuing.";
            return RedirectToAction("Index", "Positions");
        }

        var profile = GetApplicantProfile(applicant.Id);
        if (!IsApplicantProfileComplete(profile, position.IsTechnical == true))
        {
            TempData["ErrorMessage"] = "Please complete your profile before taking the questionnaire.";
            return RedirectToAction("ProfileDetails", new { positionId = position.Id });
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

        var applicant = FindOrCreateApplicantForPosition(position.CompanyId);
        if (applicant == null)
        {
            TempData["ErrorMessage"] = "Please complete your applicant profile before continuing.";
            return RedirectToAction("Index", "Positions");
        }

        var profile = GetApplicantProfile(applicant.Id);
        if (!IsApplicantProfileComplete(profile, position.IsTechnical == true))
        {
            TempData["ErrorMessage"] = "Please complete your profile before taking the questionnaire.";
            return RedirectToAction("ProfileDetails", new { positionId = position.Id });
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
        var position = _uow.Positions.Get(model.PositionId);
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

        model.PositionTitle = position.Title;
        model.IsTechnical = position.IsTechnical == true;
        model.ApplicantId = applicant.Id;
        NormalizeSelectableProfileFields(model);

        if (model.IsTechnical)
        {
            if (!model.RelevantYearsExperience.HasValue)
            {
                ModelState.AddModelError("RelevantYearsExperience", "Relevant years of experience is required for technical roles.");
            }

            if (string.IsNullOrWhiteSpace(model.Skills))
            {
                ModelState.AddModelError("Skills", "Please list your core technical skills.");
            }
        }
        

        if (!ModelState.IsValid)
        {
            PopulateLinkedInImportViewBag();
            return View("ProfileDetails", model);
        }

        applicant.FullName = model.FullName != null ? model.FullName.Trim() : applicant.FullName;
        applicant.Email = model.Email != null ? model.Email.Trim() : applicant.Email;
        applicant.Phone = model.Phone != null ? model.Phone.Trim() : applicant.Phone;

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

        profile.Location = model.Location != null ? model.Location.Trim() : null;
        profile.TotalYearsExperience = model.TotalYearsExperience;
        profile.RelevantYearsExperience = model.RelevantYearsExperience;
        profile.MostRecentCompany = model.MostRecentCompany != null ? model.MostRecentCompany.Trim() : null;
        profile.MostRecentTitle = model.MostRecentTitle != null ? model.MostRecentTitle.Trim() : null;
        profile.MostRecentStartDate = model.MostRecentStartDate;
        profile.MostRecentEndDate = model.MostRecentEndDate;
        profile.SecondMostRecentCompany = model.SecondMostRecentCompany != null ? model.SecondMostRecentCompany.Trim() : null;
        profile.SecondMostRecentTitle = model.SecondMostRecentTitle != null ? model.SecondMostRecentTitle.Trim() : null;
        profile.SecondMostRecentStartDate = model.SecondMostRecentStartDate;
        profile.SecondMostRecentEndDate = model.SecondMostRecentEndDate;
        profile.EmploymentType = model.EmploymentType != null ? model.EmploymentType.Trim() : null;
        profile.Skills = model.Skills != null ? model.Skills.Trim() : null;
        profile.Competencies = model.Competencies != null ? model.Competencies.Trim() : null;
        profile.EducationDegree = model.EducationDegree != null ? model.EducationDegree.Trim() : null;
        profile.EducationInstitution = model.EducationInstitution != null ? model.EducationInstitution.Trim() : null;
        profile.KeyAchievement = model.KeyAchievement != null ? model.KeyAchievement.Trim() : null;
        profile.Certifications = model.Certifications != null ? model.Certifications.Trim() : null;
        profile.PortfolioUrl = model.PortfolioUrl != null ? model.PortfolioUrl.Trim() : null;
        profile.WorkAuthorization = model.WorkAuthorization;
        profile.NoticePeriod = model.NoticePeriod != null ? model.NoticePeriod.Trim() : null;
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
                ViewBag.CanManageApplications = rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Applications, RoleAccessLevels.Manage);
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
                var returnUrl = Request.Url != null ? Request.Url.PathAndQuery : null;
                TempData["ReturnUrl"] = returnUrl;
                TempData["ApplicationMessage"] = "Please register or login to apply for this position.";
                return RedirectToAction("Register", "Account", new { returnUrl = returnUrl });
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
        public async Task<ActionResult> SendFailedCandidateEmail(int applicationId, string subject, string body)
        {
            var app = _uow.Applications.GetAll(a => a.Applicant, a => a.Position)
                .FirstOrDefault(a => a.Id == applicationId);
            if (app == null)
            {
                return HttpNotFound();
            }

            var tenantValidationResult = ValidateApplicationTenantAccess(app, "Access Denied");
            if (tenantValidationResult != null)
            {
                return tenantValidationResult;
            }

            var subjectValidationError = ValidateCustomEmailSubject(subject);
            if (!string.IsNullOrWhiteSpace(subjectValidationError))
            {
                TempData["ApplicationEmailError"] = subjectValidationError;
                return RedirectToAction("Index");
            }

            var messageValidationError = ValidateCustomEmailMessage(body);
            if (!string.IsNullOrWhiteSpace(messageValidationError))
            {
                TempData["ApplicationEmailError"] = messageValidationError;
                return RedirectToAction("Index");
            }

            var position = app.Position ?? _uow.Positions.Get(app.PositionId);
            if (position == null)
            {
                TempData["ApplicationEmailError"] = "Position could not be found for this application.";
                return RedirectToAction("Index");
            }

            if (!IsApplicationBelowPassMark(app, position))
            {
                TempData["ApplicationEmailError"] = "Email can only be sent from the failed-candidates list.";
                return RedirectToAction("Index");
            }

            var recipientEmail = app.Applicant != null ? app.Applicant.Email : null;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                TempData["ApplicationEmailError"] = "Candidate has no email address on file.";
                return RedirectToAction("Index");
            }

            await _email.SendAsync(recipientEmail.Trim(), subject.Trim(), BuildCustomCandidateEmailBody(subject, body));

            TempData["ApplicationEmailSuccess"] = string.Format("Email sent to {0}.", app.Applicant != null ? app.Applicant.FullName : recipientEmail.Trim());
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public async Task<ActionResult> SendFailedCandidatesBulkEmail(int positionId, string subject, string body)
        {
            if (positionId <= 0)
            {
                TempData["ApplicationEmailError"] = "Invalid position selected.";
                return RedirectToAction("Index");
            }

            var position = _uow.Positions.Get(positionId);
            if (position == null)
            {
                return HttpNotFound();
            }

            var tenantValidationResult = ValidatePositionTenantAccess(position, "Access Denied");
            if (tenantValidationResult != null)
            {
                return tenantValidationResult;
            }

            var subjectValidationError = ValidateCustomEmailSubject(subject);
            if (!string.IsNullOrWhiteSpace(subjectValidationError))
            {
                TempData["ApplicationEmailError"] = subjectValidationError;
                return RedirectToAction("Index");
            }

            var messageValidationError = ValidateCustomEmailMessage(body);
            if (!string.IsNullOrWhiteSpace(messageValidationError))
            {
                TempData["ApplicationEmailError"] = messageValidationError;
                return RedirectToAction("Index");
            }

            var failedApplications = _uow.Applications.GetAll(a => a.Applicant)
                .Where(a => a.PositionId == positionId && (a.Score ?? 0m) < position.PassMark)
                .ToList();

            if (!failedApplications.Any())
            {
                TempData["ApplicationEmailError"] = "No failed candidates found for this position.";
                return RedirectToAction("Index");
            }

            var recipients = failedApplications
                .Where(a => a.Applicant != null && !string.IsNullOrWhiteSpace(a.Applicant.Email))
                .GroupBy(a => a.Applicant.Email.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (!recipients.Any())
            {
                TempData["ApplicationEmailError"] = "No valid candidate emails found among failed candidates.";
                return RedirectToAction("Index");
            }

            var emailTasks = recipients.Select(r =>
                _email.SendAsync(
                    r.Applicant.Email.Trim(),
                    subject.Trim(),
                    BuildCustomCandidateEmailBody(subject, body)))
                .ToList();

            await Task.WhenAll(emailTasks);

            TempData["ApplicationEmailSuccess"] = string.Format(
                "Bulk email sent to {0} failed candidate{1} for {2}.",
                recipients.Count,
                recipients.Count == 1 ? string.Empty : "s",
                string.IsNullOrWhiteSpace(position.Title) ? "the selected position" : position.Title);

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
