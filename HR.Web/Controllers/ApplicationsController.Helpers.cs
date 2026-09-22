using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class ApplicationsController
    {
        private bool IsCurrentUserAuthenticated()
        {
            return User != null && User.Identity != null && User.Identity.IsAuthenticated;
        }

        private string GetApplicationsActorName()
        {
            return User?.Identity?.Name ?? "System";
        }

        private ActionResult RedirectToApplicationRegistration()
        {
            var returnUrl = Request.Url != null ? Request.Url.PathAndQuery : null;
            TempData["ReturnUrl"] = returnUrl;
            TempData["ApplicationMessage"] = "Please register or login to apply for this position.";
            return RedirectToAction("Register", "Account", new { returnUrl = returnUrl });
        }

        private Position GetPositionWithQuestions(int positionId)
        {
            return _uow.Positions.GetAll(p => p.PositionQuestions.Select(pq => pq.Question).Select(q => q.QuestionOptions))
                .FirstOrDefault(p => p.Id == positionId);
        }

        private ActionResult GetClosedPositionRedirect(Position position)
        {
            if (position.IsOpen || (User != null && User.IsInRole("Admin")))
            {
                return null;
            }

            TempData["ErrorMessage"] = "This position is no longer open for applications.";
            return RedirectToAction("Index", "Positions");
        }

        private void PopulateApplicantViewBag(int? companyId)
        {
            if (!companyId.HasValue)
            {
                return;
            }

            var user = GetCurrentUser(companyId);
            if (user == null)
            {
                return;
            }

            var applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email && a.CompanyId == companyId.Value);
            if (applicant != null)
            {
                ViewBag.Applicant = applicant;
            }
        }

        private User GetCurrentUser(int? companyId = null)
        {
            if (!IsCurrentUserAuthenticated())
            {
                return null;
            }

            var lowerUsername = User.Identity.Name.ToLower();
            var users = _uow.Users.GetAll().Where(u => u.UserName.ToLower() == lowerUsername);
            if (companyId.HasValue)
            {
                users = users.Where(u => u.CompanyId == companyId.Value);
            }

            return users.FirstOrDefault();
        }

        private ActionResult ValidatePositionTenantAccess(Position position, string accessDeniedMessage)
        {
            if (position == null)
            {
                return HttpNotFound();
            }

            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, accessDeniedMessage);
            }

            return null;
        }

        private List<PositionQuestion> GetPositionQuestions(int positionId, bool includeOptions, int? questionnaireStageNumber = null)
        {
            var query = _uow.Context.Set<PositionQuestion>()
                .Where(pq => pq.PositionId == positionId)
                .Include(pq => pq.Question);

            if (includeOptions)
            {
                query = query.Include(pq => pq.Question.QuestionOptions);
            }

            if (questionnaireStageNumber.HasValue)
            {
                var stage = questionnaireStageNumber.Value;
                query = query.Where(pq => pq.StageNumber == stage);
            }

            return query
                .OrderBy(pq => pq.Order)
                .ToList();
        }

        /// <summary>
        /// Validates tenant access, applicant identity, and profile completeness for questionnaire actions.
        /// </summary>
        private ActionResult ValidateQuestionnaireApplicantAccess(Position position, out Applicant applicant)
        {
            applicant = null;

            var tenantValidationResult = ValidatePositionTenantAccess(position, "Access Denied: Position belongs to another company.");
            if (tenantValidationResult != null)
            {
                return tenantValidationResult;
            }

            var applicantResult = RequireApplicantForPosition(position.CompanyId, out applicant);
            if (applicantResult != null)
            {
                return applicantResult;
            }

            return RequireCompleteApplicantProfile(applicant, position);
        }

        /// <summary>
        /// Validates whether the applicant may open the questionnaire for this position and which stage is active.
        /// </summary>
        private ActionResult TryValidateQuestionnaireWorkflow(int positionId, Applicant applicant, out Position position, out int activeQuestionnaireStage, out Application existingApplication)
        {
            position = null;
            activeQuestionnaireStage = 1;
            existingApplication = null;

            if (applicant == null)
            {
                TempData["ErrorMessage"] = "Please complete your applicant profile before continuing.";
                return RedirectToAction("Index", "Positions");
            }

            position = GetPositionWithQuestions(positionId);
            if (position == null)
            {
                return HttpNotFound();
            }

            var closedPositionRedirect = GetClosedPositionRedirect(position);
            if (closedPositionRedirect != null)
            {
                return closedPositionRedirect;
            }

            existingApplication = _uow.Applications.GetAll()
                .FirstOrDefault(a => a.ApplicantId == applicant.Id && a.PositionId == positionId);

            var maxStages = Math.Max(1, position.QuestionnaireStageCount);

            if (existingApplication == null)
            {
                activeQuestionnaireStage = 1;
                return null;
            }

            return ResolveExistingApplicationQuestionnaireStage(existingApplication, maxStages, out activeQuestionnaireStage);
        }

        private ActionResult ResolveExistingApplicationQuestionnaireStage(Application existingApplication, int maxStages, out int activeQuestionnaireStage)
        {
            activeQuestionnaireStage = 1;

            if (maxStages <= 1)
            {
                TempData["ErrorMessage"] = "You have already applied for this position.";
                return RedirectToAction("Index", "Positions");
            }

            if (existingApplication.LastCompletedQuestionnaireStage >= maxStages)
            {
                TempData["ErrorMessage"] = "You have already completed the questionnaire for this position.";
                return RedirectToAction("Index", "Positions");
            }

            if (existingApplication.PendingQuestionnaireStage.HasValue)
            {
                activeQuestionnaireStage = existingApplication.PendingQuestionnaireStage.Value;
                return null;
            }

            TempData["ErrorMessage"] = "You cannot access the questionnaire for this application right now. If the employer contacts you with a link to continue, use that link when you receive it.";
            return RedirectToAction("Index", "Positions");
        }

        private ApplicationReviewViewModel BuildQuestionnaireReviewModel(Position position, IEnumerable<PositionQuestion> positionQuestions, FormCollection form)
        {
            string applicantName;
            string applicantEmail;
            GetCurrentApplicantIdentity(out applicantName, out applicantEmail);

            return new ApplicationReviewViewModel
            {
                PositionId = position.Id,
                PositionTitle = position.Title,
                ApplicantName = applicantName,
                ApplicantEmail = applicantEmail,
                QuestionAnswers = BuildQuestionAnswers(positionQuestions, form)
            };
        }

        private void GetCurrentApplicantIdentity(out string applicantName, out string applicantEmail)
        {
            applicantName = null;
            applicantEmail = null;

            var user = GetCurrentUser();
            if (user == null)
            {
                return;
            }

            var applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email);
            if (applicant == null)
            {
                return;
            }

            applicantName = applicant.FullName;
            applicantEmail = applicant.Email;
        }

        private List<QuestionAnswerViewModel> BuildQuestionAnswers(IEnumerable<PositionQuestion> positionQuestions, FormCollection form)
        {
            var answers = new List<QuestionAnswerViewModel>();
            if (positionQuestions == null || form == null)
            {
                return answers;
            }

            foreach (var pq in positionQuestions)
            {
                if (pq?.Question == null)
                {
                    continue;
                }

                var answer = form["question_" + pq.Question.Id];
                answers.Add(new QuestionAnswerViewModel
                {
                    QuestionId = pq.Question.Id,
                    QuestionText = pq.Question.Text,
                    QuestionType = pq.Question.Type,
                    Answer = answer ?? ""
                });
            }

            return answers;
        }

        private void StoreQuestionnaireSession(int positionId, List<QuestionAnswerViewModel> questionAnswers, int activeQuestionnaireStage, bool acceptLegalTerms)
        {
            Session["QuestionnaireAnswers"] = questionAnswers;
            Session["PositionId"] = positionId;
            Session["QuestionnaireActiveStage"] = activeQuestionnaireStage;
            Session["LegalTermsAcceptedForApplication"] = acceptLegalTerms;
        }

        private ActionResult ValidateQuestionnaireSubmissionAccess(Position position)
        {
            var tenantValidationResult = ValidatePositionTenantAccess(position, "Access Denied");
            if (tenantValidationResult != null)
            {
                return tenantValidationResult;
            }

            return GetClosedPositionRedirect(position);
        }

        private Applicant FindOrCreateApplicantForPosition(int? targetCompanyId)
        {
            if (!targetCompanyId.HasValue)
            {
                return null;
            }

            var user = GetCurrentUser(targetCompanyId);
            if (user == null)
            {
                return null;
            }

            var applicant = _uow.Applicants.GetAll()
                .FirstOrDefault(a => a.Email == user.Email && a.CompanyId == targetCompanyId.Value);
            if (applicant != null)
            {
                return applicant;
            }

            applicant = new Applicant
            {
                FullName = string.Format("{0} {1}", user.FirstName, user.LastName),
                Email = user.Email,
                Phone = user.Phone ?? "",
                CompanyId = targetCompanyId.Value
            };
            _uow.Applicants.Add(applicant);
            _uow.Complete();
            return applicant;
        }

        private ApplicantProfile GetApplicantProfile(int applicantId)
        {
            return _uow.Context.Set<ApplicantProfile>()
                .FirstOrDefault(p => p.ApplicantId == applicantId);
        }

        private bool IsApplicantProfileComplete(ApplicantProfile profile, bool isTechnical)
        {
            if (profile == null)
            {
                return false;
            }

            if (!HasBaseProfileFields(profile))
            {
                return false;
            }

            return isTechnical
                ? HasTechnicalProfileFields(profile)
                : HasNonTechnicalProfileFields(profile);
        }

        private static bool HasBaseProfileFields(ApplicantProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var checkedProfile = profile;
            var checks = new[]
            {
                !string.IsNullOrWhiteSpace(checkedProfile.Location),
                checkedProfile.TotalYearsExperience.HasValue,
                !string.IsNullOrWhiteSpace(checkedProfile.EmploymentType),
                !string.IsNullOrWhiteSpace(checkedProfile.EducationDegree),
                !string.IsNullOrWhiteSpace(checkedProfile.EducationInstitution),
                !string.IsNullOrWhiteSpace(checkedProfile.KeyAchievement),
                !string.IsNullOrWhiteSpace(checkedProfile.NoticePeriod)
            };

            return checks.All(isValid => isValid);
        }

        private static bool HasTechnicalProfileFields(ApplicantProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var checkedProfile = profile;
            var checks = new[]
            {
                checkedProfile.RelevantYearsExperience.HasValue,
                !string.IsNullOrWhiteSpace(checkedProfile.Skills)
            };

            return checks.All(isValid => isValid);
        }

        private static bool HasNonTechnicalProfileFields(ApplicantProfile profile)
        {
            return true;
        }

        private bool HasExistingApplication(int applicantId, int positionId)
        {
            return _uow.Applications.GetAll()
                .Any(a => a.ApplicantId == applicantId && a.PositionId == positionId);
        }

        private Application CreateApplicationFromQuestionnaire(ApplicationReviewViewModel model, Position position, int applicantId, string coverLetter)
        {
            if (model == null || position == null)
            {
                throw new InvalidOperationException("Questionnaire submission requires a review model and position.");
            }

            var reviewModel = model;
            var targetPosition = position;
            var application = new Application
            {
                ApplicantId = applicantId,
                PositionId = reviewModel.PositionId,
                CompanyId = targetPosition.CompanyId,
                Status = "Interviewing",
                AppliedOn = DateTime.UtcNow,
                WorkExperienceLevel = reviewModel.YearsInRole ?? "Not specified",
                CoverLetter = coverLetter,
                CurrentStage = 1,
                LastCompletedQuestionnaireStage = 0,
                PendingQuestionnaireStage = null
            };

            _uow.Applications.Add(application);
            _uow.Complete();
            return application;
        }

        private List<QuestionAnswerViewModel> ResolveQuestionnaireAnswers(int positionId, FormCollection form)
        {
            var questionAnswers = Session["QuestionnaireAnswers"] as List<QuestionAnswerViewModel>;
            if (questionAnswers != null)
            {
                return questionAnswers;
            }

            var stage = Session["QuestionnaireActiveStage"] as int?;
            if (!stage.HasValue)
            {
                var rawStage = Session["QuestionnaireActiveStage"] as string;
                int parsedStage;
                if (!string.IsNullOrEmpty(rawStage) && int.TryParse(rawStage, out parsedStage))
                {
                    stage = parsedStage;
                }
            }

            if (!stage.HasValue)
            {
                stage = 1;
            }

            var positionQuestions = GetPositionQuestions(positionId, false, stage);
            return BuildQuestionAnswers(positionQuestions, form);
        }

        private void SaveApplicationAnswers(int applicationId, IEnumerable<QuestionAnswerViewModel> questionAnswers, int questionnaireStageNumber)
        {
            if (questionAnswers == null)
            {
                return;
            }

            foreach (var qa in questionAnswers)
            {
                if (string.IsNullOrWhiteSpace(qa.Answer))
                {
                    continue;
                }

                var answer = new ApplicationAnswer
                {
                    ApplicationId = applicationId,
                    QuestionId = qa.QuestionId,
                    AnswerText = qa.Answer,
                    StageNumber = questionnaireStageNumber
                };
                _uow.ApplicationAnswers.Add(answer);
            }

            _uow.Complete();
        }

        private void ScoreQuestionnaireApplication(Application application)
        {
            try
            {
                var score = _scoringService.CalculateApplicationScore(application);

                application.Score = score;
                application.ScoreReason = "Questionnaire score calculated from responses.";
                _uow.Applications.Update(application);
                _uow.Complete();
            }
            catch (Exception)
            {
            }
        }

        private void ClearQuestionnaireSession()
        {
            Session.Remove("QuestionnaireAnswers");
            Session.Remove("PositionId");
            Session.Remove("QuestionnaireActiveStage");
            Session.Remove("LegalTermsAcceptedForApplication");
        }

        private bool IsManagementUser(User user)
        {
            return User.IsInRole("Admin") ||
                   User.IsInRole("SuperAdmin") ||
                   user.Role == "Admin" ||
                   user.Role == "SuperAdmin";
        }

        private bool CanViewApplicationScores(User user)
        {
            return IsManagementUser(user);
        }

        private List<Application> BuildManagementApplicationsView()
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

            var interviewersQuery = _uow.Context.Users.Where(u => u.Role == "Admin").AsQueryable();
            interviewersQuery = _tenantService.ApplyTenantFilter(interviewersQuery);
            ViewBag.Interviewers = interviewersQuery.ToList();
            ViewBag.InterviewedAppIds = _uow.Context.Interviews.Select(i => i.ApplicationId).ToList();

            return apps;
        }

        private IEnumerable<Application> GetApplicantApplications(User user)
        {
            var applicant = _uow.Context.Applicants
                .FirstOrDefault(a => a.Email == user.Email && a.CompanyId == user.CompanyId);
            if (applicant == null)
            {
                return Enumerable.Empty<Application>();
            }

            return _uow.Context.Applications
                .Include("Applicant")
                .Include("Position")
                .Where(a => a.ApplicantId == applicant.Id)
                .OrderByDescending(a => a.AppliedOn)
                .ToList();
        }

        private ActionResult ValidateDetailsAccess(User user, Application app)
        {
            if (IsManagementUser(user))
            {
                var companyId = _tenantService.GetCurrentUserCompanyId();
                if (companyId.HasValue && app.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
                {
                    return new HttpStatusCodeResult(403, "Access Denied: Application belongs to another company context.");
                }

                return null;
            }

            var applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email);
            if (applicant == null || app.ApplicantId != applicant.Id)
            {
                return new HttpStatusCodeResult(403, "Access Denied: You may only view your own applications.");
            }

            return null;
        }

        private string ValidateApplicationOwnership(Application model)
        {
            if (!IsCurrentUserAuthenticated())
            {
                return "You must be signed in to create an application.";
            }

            if (User.IsInRole("Admin"))
            {
                return null;
            }

            var user = GetCurrentUser();
            if (user == null)
            {
                return "User record not found.";
            }

            var applicant = _uow.Applicants.GetAll().FirstOrDefault(a => a.Email == user.Email);
            if (applicant == null)
            {
                return "No applicant profile matched to your account.";
            }

            if (model.ApplicantId != applicant.Id)
            {
                return "You may only apply using your own applicant profile.";
            }

            return null;
        }

        private bool TryAssignAndValidateApplicationCompany(Application model)
        {
            var position = _uow.Positions.Get(model.PositionId);
            if (position != null)
            {
                model.CompanyId = position.CompanyId;
            }

            var companyId = _tenantService.GetCurrentUserCompanyId();
            return !companyId.HasValue || model.CompanyId == companyId.Value || _tenantService.IsSuperAdmin();
        }

        private ActionResult ValidateApplicationTenantAccess(Application app, string accessDeniedMessage)
        {
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && app.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, accessDeniedMessage);
            }

            return null;
        }

        private static string ValidateCustomEmailSubject(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                return "Please enter an email subject before sending.";
            }

            var trimmed = subject.Trim();
            if (trimmed.Length > 255)
            {
                return "Subject is too long. Maximum length is 255 characters.";
            }

            return null;
        }

        private string ValidateCustomEmailMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Please enter an email body before sending.";
            }

            var trimmed = message.Trim();
            if (trimmed.Length > 4000)
            {
                return "Message is too long. Maximum length is 4000 characters.";
            }

            return null;
        }

        private object GetApplicationFlowRouteValues(int positionId)
        {
            if (HttpContext?.Request?.RequestContext?.RouteData?.Values == null)
            {
                return new { positionId = positionId };
            }

            var tenant = HttpContext.Request.RequestContext.RouteData.Values["tenant"] as string;
            if (string.IsNullOrWhiteSpace(tenant))
            {
                return new { positionId = positionId };
            }

            return new { tenant = tenant, positionId = positionId };
        }

        private void NormalizePortfolioUrlField(ApplicantProfileViewModel model)
        {
            if (model == null)
            {
                return;
            }

            ModelState.Remove("PortfolioUrl");
            var raw = (Request["PortfolioUrl"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                model.PortfolioUrl = null;
                return;
            }

            Uri parsed;
            if (Uri.TryCreate(raw, UriKind.Absolute, out parsed))
            {
                model.PortfolioUrl = parsed;
                return;
            }

            ModelState.AddModelError("PortfolioUrl", "Enter a valid URL starting with https://, or leave blank.");
        }

        private void NormalizeOptionalEmploymentHistoryFields(ApplicantProfileViewModel model)
        {
            if (model == null)
            {
                return;
            }

            model.MostRecentCompany = TrimProfileText(model.MostRecentCompany);
            model.MostRecentTitle = TrimProfileText(model.MostRecentTitle);
            model.SecondMostRecentCompany = TrimProfileText(model.SecondMostRecentCompany);
            model.SecondMostRecentTitle = TrimProfileText(model.SecondMostRecentTitle);

            foreach (var field in new[]
            {
                "MostRecentCompany",
                "MostRecentTitle",
                "MostRecentStartDate",
                "MostRecentEndDate",
                "SecondMostRecentCompany",
                "SecondMostRecentTitle",
                "SecondMostRecentStartDate",
                "SecondMostRecentEndDate"
            })
            {
                ModelState.Remove(field);
            }
        }

        private static string TrimProfileText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private void NormalizeSelectableProfileFields(ApplicantProfileViewModel model)
        {
            if (model == null)
            {
                return;
            }

            NormalizeSelectableRequiredTextField(
                model,
                "EmploymentType",
                "employmentTypeSelect",
                "employmentTypeCustom",
                "Please specify your employment type.");
            NormalizeSelectableRequiredTextField(
                model,
                "NoticePeriod",
                "noticePeriodSelect",
                "noticePeriodCustom",
                "Please specify your notice period / availability.");
            NormalizeSelectableRequiredTextField(
                model,
                "EducationDegree",
                "educationDegreeSelect",
                "educationDegreeCustom",
                "Please specify your education degree.");
        }

        private void NormalizeSelectableRequiredTextField(
            ApplicantProfileViewModel model,
            string modelPropertyName,
            string selectFieldName,
            string customFieldName,
            string requiredMessage)
        {
            var selectedValue = (Request[selectFieldName] ?? string.Empty).Trim();
            var customValue = (Request[customFieldName] ?? string.Empty).Trim();
            ModelState.Remove(modelPropertyName);

            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                ModelState.AddModelError(modelPropertyName, requiredMessage);
                return;
            }

            var resolvedValue = string.Equals(selectedValue, "__custom__", StringComparison.OrdinalIgnoreCase)
                ? customValue
                : selectedValue;

            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                ModelState.AddModelError(modelPropertyName, requiredMessage);
                return;
            }

            switch (modelPropertyName)
            {
                case "EmploymentType":
                    model.EmploymentType = resolvedValue;
                    break;
                case "NoticePeriod":
                    model.NoticePeriod = resolvedValue;
                    break;
                case "EducationDegree":
                    model.EducationDegree = resolvedValue;
                    break;
            }
        }

        private ActionResult TryGetManagedApplication(int id, out Application application)
        {
            application = _uow.Applications.Get(id);
            if (application == null)
            {
                return HttpNotFound();
            }

            return ValidateApplicationTenantAccess(application, "Access Denied");
        }

        private ActionResult RequireApplicantForPosition(int? companyId, out Applicant applicant)
        {
            applicant = FindOrCreateApplicantForPosition(companyId);
            if (applicant != null)
            {
                return null;
            }

            TempData["ErrorMessage"] = "Please complete your applicant profile before continuing.";
            return RedirectToAction("Index", "Positions");
        }

        private ActionResult RequireCompleteApplicantProfile(Applicant applicant, Position position)
        {
            var profile = GetApplicantProfile(applicant.Id);
            if (IsApplicantProfileComplete(profile, position.IsTechnical == true))
            {
                return null;
            }

            TempData["ErrorMessage"] = "Please complete your profile before taking the questionnaire.";
            return RedirectToAction("ProfileDetails", new { positionId = position.Id });
        }

        private static bool IsApplicationBelowPassMark(Application application, Position position)
        {
            if (application == null || position == null)
            {
                return false;
            }

            return (application.Score ?? 0m) < position.PassMark;
        }

        private static string BuildCustomCandidateEmailBody(string emailSubject, string customMessage)
        {
            var safeSubject = HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(emailSubject) ? "Application Update" : emailSubject.Trim());
            var safeMessage = HttpUtility.HtmlEncode((customMessage ?? string.Empty).Trim())
                .Replace("\r\n", "<br/>")
                .Replace("\n", "<br/>");

            return string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>{0}</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <div>{1}</div>
</body>
</html>", safeSubject, safeMessage);
        }

        private ApplicantProfileViewModel BuildApplicantProfileViewModel(Position position, Applicant applicant, ApplicantProfile profile)
        {
            return new ApplicantProfileViewModel
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
        }

        private void ApplyApplicantProfileViewModel(Applicant applicant, ApplicantProfile profile, ApplicantProfileViewModel profileModel)
        {
            applicant.FullName = profileModel.FullName != null ? profileModel.FullName.Trim() : applicant.FullName;
            applicant.Email = profileModel.Email != null ? profileModel.Email.Trim() : applicant.Email;
            applicant.Phone = profileModel.Phone != null ? profileModel.Phone.Trim() : applicant.Phone;

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
            profile.PortfolioUrl = profileModel.PortfolioUrl;
            profile.WorkAuthorization = profileModel.WorkAuthorization;
            profile.NoticePeriod = profileModel.NoticePeriod != null ? profileModel.NoticePeriod.Trim() : null;
            profile.UpdatedOn = DateTime.UtcNow;
        }

        private void ValidateTechnicalProfileFields(ApplicantProfileViewModel profileModel)
        {
            if (!profileModel.IsTechnical)
            {
                return;
            }

            if (!profileModel.RelevantYearsExperience.HasValue)
            {
                ModelState.AddModelError("RelevantYearsExperience", "Relevant years of experience is required for technical roles.");
            }

            if (string.IsNullOrWhiteSpace(profileModel.Skills))
            {
                ModelState.AddModelError("Skills", "Please list your core technical skills.");
            }
        }

        private int ResolveQuestionnaireSessionStage()
        {
            var sessionStageObj = Session["QuestionnaireActiveStage"] as int?;
            if (sessionStageObj.HasValue)
            {
                return sessionStageObj.Value;
            }

            var rawStage = Session["QuestionnaireActiveStage"] as string;
            int parsedStage;
            if (!string.IsNullOrEmpty(rawStage) && int.TryParse(rawStage, out parsedStage))
            {
                return parsedStage;
            }

            return 1;
        }

        private ActionResult SubmitInitialQuestionnaireApplication(
            ApplicationReviewViewModel reviewModel,
            Position position,
            Applicant applicant,
            FormCollection form)
        {
            if (HasExistingApplication(applicant.Id, reviewModel.PositionId))
            {
                TempData["ErrorMessage"] = "You have already applied for this position.";
                return RedirectToAction("Index", "Positions");
            }

            var coverLetter = GetPendingCoverLetter(reviewModel.PositionId);
            if (string.IsNullOrWhiteSpace(coverLetter))
            {
                return RedirectToCoverLetter(reviewModel.PositionId);
            }

            var application = CreateApplicationFromQuestionnaire(reviewModel, position, applicant.Id, coverLetter);
            var questionAnswers = ResolveQuestionnaireAnswers(reviewModel.PositionId, form);
            SaveApplicationAnswers(application.Id, questionAnswers, 1);
            application.LastCompletedQuestionnaireStage = 1;
            application.PendingQuestionnaireStage = null;
            _uow.Applications.Update(application);
            _uow.Complete();
            ClearPendingCoverLetter();
            ScoreQuestionnaireApplication(application);
            SendApplicationReceivedStandardEmail(application, applicant, position);
            return null;
        }

        private ActionResult SubmitPendingQuestionnaireStage(
            Application existingApplication,
            ApplicationReviewViewModel reviewModel,
            int sessionStage,
            FormCollection form)
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
            return null;
        }

        private void SendApplicationReceivedStandardEmail(Application application, Applicant applicant, Position position)
        {
            if (application == null || applicant == null || position == null)
            {
                return;
            }

            var recipientEmail = applicant.Email;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return;
            }

            try
            {
                var company = application.CompanyId.HasValue
                    ? _uow.Companies.Get(application.CompanyId.Value)
                    : null;
                var companyName = company != null && !string.IsNullOrWhiteSpace(company.Name)
                    ? company.Name.Trim()
                    : "Recruitment Team";
                var candidateName = !string.IsNullOrWhiteSpace(applicant.FullName)
                    ? applicant.FullName.Trim()
                    : "Candidate";
                var positionTitle = string.IsNullOrWhiteSpace(position.Title)
                    ? "this position"
                    : position.Title.Trim();

                var rendered = _emailTemplateService.Render(
                    EmailTemplateCatalog.ApplicationReceivedStandard,
                    new Dictionary<string, string>
                    {
                        { "CandidateName", HttpUtility.HtmlEncode(candidateName) },
                        { "PositionTitle", HttpUtility.HtmlEncode(positionTitle) },
                        { "CompanyName", HttpUtility.HtmlEncode(companyName) },
                        { "CustomMessageBlock", string.Empty }
                    },
                    application.CompanyId);

                if (rendered == null)
                {
                    System.Diagnostics.Trace.WriteLine(
                        "ApplicationReceivedStandard template could not be rendered for application " + application.Id);
                    return;
                }

                _email.SendAsync(
                    recipientEmail.Trim(),
                    rendered.Subject ?? "Application received",
                    WrapApplicationReceivedEmailBody(rendered.BodyHtml ?? string.Empty)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(
                    "ApplicationReceivedStandard email failed for application " + application.Id + ": " + ex.Message);
            }
        }

        private static string WrapApplicationReceivedEmailBody(string innerHtml)
        {
            return string.Format(
                @"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""/></head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
{0}
</body>
</html>",
                innerHtml ?? string.Empty);
        }

        private void PopulateApplicationDetailsViewBag(User user, Application app)
        {
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
        }
    }
}
