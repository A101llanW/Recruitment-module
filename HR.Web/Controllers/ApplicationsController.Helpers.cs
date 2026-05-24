using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class ApplicationsController
    {
        private bool IsCurrentUserAuthenticated()
        {
            return User != null && User.Identity != null && User.Identity.IsAuthenticated;
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

        private void LogPositionQuestions(Position position)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("=== Position {0} Questions ===", position.Title));
            foreach (var pq in position.PositionQuestions)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Question: {0} (Type: {1})", pq.Question.Text, pq.Question.Type));
                var optionCount = pq.Question.QuestionOptions != null ? pq.Question.QuestionOptions.Count() : 0;
                System.Diagnostics.Debug.WriteLine(string.Format("Options count: {0}", optionCount));
                if (pq.Question.QuestionOptions == null)
                {
                    continue;
                }

                foreach (var option in pq.Question.QuestionOptions)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("  - Option: {0} (Points: {1})", option.Text, option.Points));
                }
            }
            System.Diagnostics.Debug.WriteLine("=== End Questions ===");
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
            foreach (var pq in positionQuestions)
            {
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

        private bool TrySaveResumeForQuestionnaire(HttpPostedFileBase resume, out string resumePath, out string errorMessage)
        {
            resumePath = null;
            errorMessage = null;

            System.Diagnostics.Debug.WriteLine("=== DEBUG: Resume Upload ===");
            System.Diagnostics.Debug.WriteLine(string.Format("Resume is null: {0}", resume == null));
            if (resume != null)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Resume ContentLength: {0}", resume.ContentLength));
                System.Diagnostics.Debug.WriteLine(string.Format("Resume FileName: {0}", resume.FileName));
            }
            System.Diagnostics.Debug.WriteLine("=== END DEBUG ===");

            if (resume == null || resume.ContentLength <= 0)
            {
                // Resume/CV upload is optional.
                return true;
            }

            if (resume.ContentLength > 5 * 1024 * 1024)
            {
                errorMessage = "Resume file size must be less than 5MB.";
                return false;
            }

            var fileExtension = System.IO.Path.GetExtension(resume.FileName).ToLower();
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            if (!allowedExtensions.Contains(fileExtension))
            {
                errorMessage = "Only PDF, DOC, and DOCX files are allowed.";
                return false;
            }

            try
            {
                resumePath = _storage.SaveResume(resume);
                System.Diagnostics.Debug.WriteLine("Resume saved to: " + resumePath);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Error uploading resume: " + ex.Message;
                return false;
            }
        }

        private void StoreQuestionnaireSession(int positionId, List<QuestionAnswerViewModel> questionAnswers, string resumePath, int activeQuestionnaireStage, bool acceptLegalTerms)
        {
            Session["QuestionnaireAnswers"] = questionAnswers;
            Session["PositionId"] = positionId;
            Session["ResumePath"] = resumePath;
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
            var checks = new[]
            {
                !string.IsNullOrWhiteSpace(profile.Location),
                profile.TotalYearsExperience.HasValue,
                !string.IsNullOrWhiteSpace(profile.MostRecentCompany),
                !string.IsNullOrWhiteSpace(profile.MostRecentTitle),
                profile.MostRecentStartDate.HasValue,
                !string.IsNullOrWhiteSpace(profile.EmploymentType),
                !string.IsNullOrWhiteSpace(profile.EducationDegree),
                !string.IsNullOrWhiteSpace(profile.EducationInstitution),
                !string.IsNullOrWhiteSpace(profile.KeyAchievement),
                !string.IsNullOrWhiteSpace(profile.NoticePeriod)
            };

            return checks.All(isValid => isValid);
        }

        private static bool HasTechnicalProfileFields(ApplicantProfile profile)
        {
            var checks = new[]
            {
                profile.RelevantYearsExperience.HasValue,
                !string.IsNullOrWhiteSpace(profile.Skills)
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

        private Application CreateApplicationFromQuestionnaire(ApplicationReviewViewModel model, Position position, int applicantId)
        {
            var resumePath = Session["ResumePath"] as string;
            var application = new Application
            {
                ApplicantId = applicantId,
                PositionId = model.PositionId,
                CompanyId = position.CompanyId,
                Status = "Interviewing",
                AppliedOn = DateTime.UtcNow,
                WorkExperienceLevel = model.YearsInRole ?? "Not specified",
                ResumePath = resumePath ?? model.ResumePath,
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
                System.Diagnostics.Debug.WriteLine("Error scoring application: " + ex.Message);
            }
        }

        private void ClearQuestionnaireSession()
        {
            Session.Remove("QuestionnaireAnswers");
            Session.Remove("PositionId");
            Session.Remove("ResumePath");
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
            if (!IsCurrentUserAuthenticated() || User.IsInRole("Admin"))
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

        private string ValidateCustomEmailSubject(string subject)
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
            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                return;
            }

            ModelState.Remove(modelPropertyName);

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
    }
}
