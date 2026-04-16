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
    public class ApplicationsController : Controller
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
        // Check if user is authenticated
        if (User == null || !User.Identity.IsAuthenticated)
        {
            // Store the position they want to apply for
            TempData["ReturnUrl"] = Request.Url != null ? Request.Url.ToString() : null;
            TempData["ApplicationMessage"] = "Please register or login to apply for this position.";
            return RedirectToAction("Register", "Account");
        }

        var position = _uow.Positions.GetAll(p => p.PositionQuestions.Select(pq => pq.Question).Select(q => q.QuestionOptions))
            .FirstOrDefault(p => p.Id == positionId);
        if (position == null)
            return HttpNotFound();
        
        // Prevent non-admin users from accessing closed positions
        if (!position.IsOpen && (User == null || !User.IsInRole("Admin")))
        {
            TempData["ErrorMessage"] = "This position is no longer open for applications.";
            return RedirectToAction("Index", "Positions");
        }
        
        // Debug: Log questions and their options
        System.Diagnostics.Debug.WriteLine(string.Format("=== Position {0} Questions ===", position.Title));
        foreach (var pq in position.PositionQuestions)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("Question: {0} (Type: {1})", pq.Question.Text, pq.Question.Type));
            System.Diagnostics.Debug.WriteLine(string.Format("Options count: {0}", pq.Question.QuestionOptions != null ? pq.Question.QuestionOptions.Count() : 0));
            if (pq.Question.QuestionOptions != null)
            {
                foreach (var option in pq.Question.QuestionOptions)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("  - Option: {0} (Points: {1})", option.Text, option.Points));
                }
            }
        }
        System.Diagnostics.Debug.WriteLine("=== End Questions ===");
        
        ViewBag.Position = position;
        // Autofill applicant info from logged-in user
        if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
        {
            var companyId = position.CompanyId;
            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            
            // Find user in the position's company context
            var user = _uow.Users.GetAll().FirstOrDefault(u => u.UserName.ToLower() == lowerUsername && u.CompanyId == companyId);
            
            if (user != null)
            {
                // Find applicant record ONLY for THIS company
                var applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email && a.CompanyId == companyId);
                if (applicant != null)
                {
                    ViewBag.Applicant = applicant;
                }
            }
        }
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
            return HttpNotFound();

        // Check if position belongs to user's company (if logged in)
        var companyId = _tenantService.GetCurrentUserCompanyId();
        if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
        {
            return new HttpStatusCodeResult(403, "Access Denied: Position belongs to another company.");
        }

        // Get position questions
        var positionQuestions = _uow.Context.Set<PositionQuestion>()
            .Where(pq => pq.PositionId == positionId)
            .Include(pq => pq.Question)
            .Include(pq => pq.Question.QuestionOptions)
            .OrderBy(pq => pq.Order)
            .ToList();

        // Determine applicant info for display
        string applicantName = null;
        string applicantEmail = null;
        if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
        {
            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            var user = _uow.Users.GetAll().FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
            if (user != null)
            {
                var applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email);
                if (applicant != null)
                {
                    applicantName = applicant.FullName;
                    applicantEmail = applicant.Email;
                }
            }
        }

        // Create application review model
        var review = new ApplicationReviewViewModel
        {
            PositionId = positionId,
            PositionTitle = position.Title,
            ApplicantName = applicantName,
            ApplicantEmail = applicantEmail,
            QuestionAnswers = new List<QuestionAnswerViewModel>()
        };

        // Process dynamic question answers
        foreach (var pq in positionQuestions)
        {
            var questionFieldName = "question_" + pq.Question.Id;
            var answer = form[questionFieldName];
            
            review.QuestionAnswers.Add(new QuestionAnswerViewModel
            {
                QuestionId = pq.Question.Id,
                QuestionText = pq.Question.Text,
                QuestionType = pq.Question.Type,
                Answer = answer ?? ""
            });
        }

        // Handle resume upload
        string resumePath = null;
        System.Diagnostics.Debug.WriteLine("=== DEBUG: Resume Upload ===");
        System.Diagnostics.Debug.WriteLine(string.Format("Resume is null: {0}", resume == null));
        if (resume != null)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("Resume ContentLength: {0}", resume.ContentLength));
            System.Diagnostics.Debug.WriteLine(string.Format("Resume FileName: {0}", resume.FileName));
        }
        System.Diagnostics.Debug.WriteLine("=== END DEBUG ===");
        
        if (resume != null && resume.ContentLength > 0)
        {
            // Validate file size (5MB max)
            if (resume.ContentLength > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Resume file size must be less than 5MB.";
                return RedirectToAction("Questionnaire", new { positionId = positionId });
            }

            // Validate file type
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            var fileExtension = System.IO.Path.GetExtension(resume.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                TempData["ErrorMessage"] = "Only PDF, DOC, and DOCX files are allowed.";
                return RedirectToAction("Questionnaire", new { positionId = positionId });
            }

            try
            {
                resumePath = _storage.SaveResume(resume);
                System.Diagnostics.Debug.WriteLine("Resume saved to: " + resumePath);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error uploading resume: " + ex.Message;
                return RedirectToAction("Questionnaire", new { positionId = positionId });
            }
        }
        else
        {
            // CV required
            TempData["ErrorMessage"] = "Please upload your CV/Resume to continue.";
            return RedirectToAction("Questionnaire", new { positionId = positionId });
        }

        // Store answers in session for processing
        Session["QuestionnaireAnswers"] = review.QuestionAnswers;
        Session["PositionId"] = positionId;
        Session["ResumePath"] = resumePath;

        // Update review model with resume path
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

        // Validate that position is still open
        var position = _uow.Positions.Get(model.PositionId);
        if (position == null)
        {
            TempData["ErrorMessage"] = "Position not found.";
            return RedirectToAction("Index", "Positions");
        }

        // Check if position belongs to user's company (if logged in)
        var companyId = _tenantService.GetCurrentUserCompanyId();
        if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
        {
            return new HttpStatusCodeResult(403, "Access Denied");
        }
        
        // Prevent non-admin users from applying to closed positions
        if (!position.IsOpen && (User == null || !User.IsInRole("Admin")))
        {
            TempData["ErrorMessage"] = "This position is no longer open for applications.";
            return RedirectToAction("Index", "Positions");
        }

        // Find or create applicant from logged-in user
        Applicant applicant = null;
        if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
        {
            var targetCompanyId = position.CompanyId;
            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            
            // Find user in the position's company context
            var user = _uow.Users.GetAll().FirstOrDefault(u => u.UserName.ToLower() == lowerUsername && u.CompanyId == targetCompanyId);
            
            if (user != null)
            {
                // Find/Create applicant record ONLY for THIS company (tenant isolation)
                applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email && a.CompanyId == targetCompanyId);
                if (applicant == null)
                {
                    // Create new applicant from user info for this company
                    applicant = new Applicant
                    {
                        FullName = string.Format("{0} {1}", user.FirstName, user.LastName),
                        Email = user.Email,
                        Phone = user.Phone ?? "",
                        CompanyId = targetCompanyId // Assign to position's company
                    };
                    _uow.Applicants.Add(applicant);
                    _uow.Complete();
                }
            }
        }

        if (applicant != null)
        {
            // Check if applicant has already applied for this position
            var existingApplication = _uow.Applications.GetAll()
                .FirstOrDefault(a => a.ApplicantId == applicant.Id && a.PositionId == model.PositionId);
            if (existingApplication != null)
            {
                TempData["ErrorMessage"] = "You have already applied for this position.";
                return RedirectToAction("Index", "Positions");
            }
            
            // Get resume path from session
        var resumePath = Session["ResumePath"] as string;
        
            var application = new Application
            {
                ApplicantId = applicant.Id,
                PositionId = model.PositionId,
                CompanyId = position.CompanyId, // Assign to tenant
                Status = "Interviewing",
                AppliedOn = DateTime.UtcNow,
                WorkExperienceLevel = model.YearsInRole ?? "Not specified",
                ResumePath = resumePath ?? model.ResumePath
            };
            _uow.Applications.Add(application);
            _uow.Complete();

            // Process dynamic answers from session OR form
            var applicationAnswers = new List<ApplicationAnswer>();
            var questionAnswers = Session["QuestionnaireAnswers"] as List<QuestionAnswerViewModel>;
            
            // If session is empty, try to get from form directly
            if (questionAnswers == null)
            {
                // Get position questions to process form
                var positionQuestions = _uow.Context.Set<PositionQuestion>()
                    .Where(pq => pq.PositionId == model.PositionId)
                    .Include(pq => pq.Question)
                    .OrderBy(pq => pq.Order)
                    .ToList();

                questionAnswers = new List<QuestionAnswerViewModel>();
                foreach (var pq in positionQuestions)
                {
                    var questionFieldName = "question_" + pq.Question.Id;
                    var answer = form[questionFieldName];
                    
                    questionAnswers.Add(new QuestionAnswerViewModel
                    {
                        QuestionId = pq.Question.Id,
                        QuestionText = pq.Question.Text,
                        QuestionType = pq.Question.Type,
                        Answer = answer ?? ""
                    });
                }
            }
            
            if (questionAnswers != null)
            {
                foreach (var qa in questionAnswers)
                {
                    if (!string.IsNullOrWhiteSpace(qa.Answer))
                    {
                        var appAns = new ApplicationAnswer
                        {
                            ApplicationId = application.Id,
                            QuestionId = qa.QuestionId,
                            AnswerText = qa.Answer
                        };
                        _uow.ApplicationAnswers.Add(appAns);
                        applicationAnswers.Add(appAns);
                    }
                }
            }
            _uow.Complete();

            // Evaluate candidate using questionnaire scoring
            try
            {
                System.Diagnostics.Debug.WriteLine("=== STARTING QUESTIONNAIRE SCORING ===");
                System.Diagnostics.Debug.WriteLine("Application ID: " + application.Id);

                var score = _scoringService.CalculateApplicationScore(application);

                System.Diagnostics.Debug.WriteLine("QUESTIONNAIRE SCORE: " + score);
                System.Diagnostics.Debug.WriteLine("SETTING APPLICATION SCORE TO: " + score);

                application.Score = score;
                application.ScoreReason = "Questionnaire score calculated from responses.";
                _uow.Applications.Update(application);
                _uow.Complete();
                
                System.Diagnostics.Debug.WriteLine("QUESTIONNAIRE SCORE SAVED TO DATABASE");
                System.Diagnostics.Debug.WriteLine("===============================");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the application submission
                System.Diagnostics.Debug.WriteLine("Error scoring application: " + ex.Message);
            }
        }

        // Clean up session
        Session.Remove("QuestionnaireAnswers");
        Session.Remove("PositionId");
        Session.Remove("ResumePath");

        TempData["QuestionnaireSuccess"] = "Your application and questionnaire have been submitted.";
        return RedirectToAction("Index", "Positions");
    }

        public ActionResult Index()
        {
            // If the user is unauthenticated, show the guest redirect message
            if (User == null || !User.Identity.IsAuthenticated)
            {
                ViewBag.Message = "Please sign in or create account first to view your applications.";
                return View("GuestAccess");
            }

            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            var user = _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
            if (user == null) return View(Enumerable.Empty<Application>());

            // Consolidated check for management roles (Admin or SuperAdmin)
            bool isManagement = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || user.Role == "Admin" || user.Role == "SuperAdmin";

            // If the user is Admin or SuperAdmin, show all applications for their context
            if (isManagement)
            {
                var appsQuery = _uow.Context.Applications
                    .Include("Applicant")
                    .Include("Position")
                    .AsQueryable();

                appsQuery = _tenantService.ApplyTenantFilter(appsQuery);
                var apps = appsQuery
                    .OrderByDescending(a => a.Score ?? 0)
                    .ThenByDescending(a => a.AppliedOn)
                    .ToList();
                
                // Get interviewers for booking (filtered by tenant)
                var interviewersQuery = _uow.Context.Users.Where(u => u.Role == "Admin").AsQueryable();
                interviewersQuery = _tenantService.ApplyTenantFilter(interviewersQuery);
                ViewBag.Interviewers = interviewersQuery.ToList();
                
                // Get existing interview application IDs
                var interviewedAppIds = _uow.Context.Interviews.Select(i => i.ApplicationId).ToList();
                ViewBag.InterviewedAppIds = interviewedAppIds;
                
                return View(apps);
            }

            // Otherwise, show only applications for the logged-in applicant (Client role) for the CURRENT tenant
            var applicant = _uow.Context.Applicants.FirstOrDefault(a => a.Email == user.Email && a.CompanyId == user.CompanyId);
            if (applicant != null)
            {
                var apps = _uow.Context.Applications
                    .Include("Applicant")
                    .Include("Position")
                    .Where(a => a.ApplicantId == applicant.Id)
                    .OrderByDescending(a => a.AppliedOn)
                    .ToList();
                return View(apps);
            }

            // If not matched, show empty list
            return View(Enumerable.Empty<Application>());
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

            // Security check: Who can view this application?
            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            var user = _uow.Users.GetAll().FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
            if (user == null) return new HttpStatusCodeResult(403, "Access Denied");

            bool isManagement = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || user.Role == "Admin" || user.Role == "SuperAdmin";
            
            if (isManagement)
            {
                // SuperAdmin sees all, Admin sees their company
                var companyId = _tenantService.GetCurrentUserCompanyId();
                if (companyId.HasValue && app.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
                {
                    return new HttpStatusCodeResult(403, "Access Denied: Application belongs to another company context.");
                }
            }
            else
            {
                // Client can only see their own application
                var applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email);
                if (applicant == null || app.ApplicantId != applicant.Id)
                {
                    return new HttpStatusCodeResult(403, "Access Denied: You may only view your own applications.");
                }
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
            // Server-side checks: if user is regular (not Admin/HR), ensure they are applying as themselves
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated && !User.IsInRole("Admin"))
            {
                var username = User.Identity.Name;
                var lowerUsername = username.ToLower();
                var user = _uow.Users.GetAll().FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
                if (user == null)
                {
                    ModelState.AddModelError("", "User record not found.");
                    LoadLookups(model);
                    return View(model);
                }
                var applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email);
                if (applicant == null)
                {
                    ModelState.AddModelError("", "No applicant profile matched to your account.");
                    LoadLookups(model);
                    return View(model);
                }
                if (model.ApplicantId != applicant.Id)
                {
                    ModelState.AddModelError("", "You may only apply using your own applicant profile.");
                    LoadLookups(model);
                    return View(model);
                }
            }

            if (!ModelState.IsValid)
            {
                LoadLookups(model);
                return View(model);
            }

            model.AppliedOn = DateTime.UtcNow;
            
            // Assign company from position
            var position = _uow.Positions.Get(model.PositionId);
            if (position != null)
            {
                model.CompanyId = position.CompanyId;
            }
            // If logged in as admin, verify tenant match
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && model.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
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






