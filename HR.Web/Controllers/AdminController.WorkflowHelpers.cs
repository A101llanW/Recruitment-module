using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.ViewModels;
using Newtonsoft.Json;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        private ActionResult HandleCandidateRankings(int? positionId)
        {
            var applications = GetCandidateRankingApplications(positionId);
            var viewModel = new CandidateRankingsViewModel
            {
                Positions = _uow.Positions.GetAll(p => p.Department).ToList(),
                CandidatesByPosition = BuildCandidatesByPosition(applications)
            };

            ViewBag.SelectedPositionId = positionId;
            return View(viewModel);
        }

        private List<Application> GetCandidateRankingApplications(int? positionId)
        {
            var applicationsQuery = _uow.Applications.GetAll(
                a => a.Applicant,
                a => a.Position,
                a => a.Position.Department
            ).AsQueryable();

            applicationsQuery = _tenantService.ApplyTenantFilter(applicationsQuery);
            if (positionId.HasValue)
            {
                applicationsQuery = applicationsQuery.Where(a => a.PositionId == positionId.Value);
            }

            return applicationsQuery.ToList();
        }

        private Dictionary<Position, List<CandidateApplicationScore>> BuildCandidatesByPosition(IEnumerable<Application> applications)
        {
            var candidatesByPosition = new Dictionary<Position, List<CandidateApplicationScore>>();
            foreach (var application in applications)
            {
                if (application.Position == null)
                {
                    continue;
                }

                if (!candidatesByPosition.TryGetValue(application.Position, out var scores))
                {
                    scores = new List<CandidateApplicationScore>();
                    candidatesByPosition[application.Position] = scores;
                }

                scores.Add(BuildCandidateScore(application));
            }

            foreach (var position in candidatesByPosition.Keys.ToList())
            {
                candidatesByPosition[position] = candidatesByPosition[position]
                    .OrderByDescending(c => c.TotalScore)
                    .ToList();
            }

            return candidatesByPosition;
        }

        private CandidateApplicationScore BuildCandidateScore(Application application)
        {
            var questionnaireScore = CalculateQuestionnaireScoreOrFallback(application);
            return new CandidateApplicationScore
            {
                ApplicationId = application.Id,
                CandidateName = application.Applicant != null ? application.Applicant.FullName : "Unknown",
                CandidateEmail = application.Applicant != null ? application.Applicant.Email : "",
                TotalScore = questionnaireScore,
                QuestionnaireScore = questionnaireScore,
                MaxQuestionnaireScore = 100,
                AppliedDate = application.AppliedOn,
                Status = application.Status ?? "Pending",
                PositionId = application.PositionId
            };
        }

        private decimal CalculateQuestionnaireScoreOrFallback(Application application)
        {
            try
            {
                return _scoringService.CalculateApplicationScore(application);
            }
            catch
            {
                return application.Score ?? 0;
            }
        }

        private ActionResult HandleEditQuestion(QuestionAdminViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var isUpdate = model.Id.HasValue;
            try
            {
                Question question;
                object oldValues;
                var prepareResult = TryPrepareQuestionForSave(model, isUpdate, out question, out oldValues);
                if (prepareResult != null)
                {
                    return prepareResult;
                }

                var options = string.Equals(model.Type, "Choice", StringComparison.OrdinalIgnoreCase)
                    ? SaveQuestionOptions(question.Id, model.Options)
                    : SaveQuestionOptions(question.Id, Enumerable.Empty<QuestionOptionVM>());
                LogQuestionSaved(question, oldValues, options, isUpdate);
                TempData["Message"] = "Question saved.";
            }
            catch (Exception ex)
            {
                var action = isUpdate ? "UPDATE" : "CREATE";
                _auditService.LogAction(User.Identity.Name, action, "Admin",
                    model.Id.HasValue ? model.Id.Value.ToString() : "new",
                    wasSuccessful: false, errorMessage: ex.Message);
                TempData["Error"] = "Error saving question: " + ex.Message;
            }

            return RedirectToAction("Questions");
        }

        private ActionResult TryPrepareQuestionForSave(QuestionAdminViewModel model, bool isUpdate, out Question question, out object oldValues)
        {
            question = null;
            oldValues = new object();

            if (isUpdate)
            {
                question = _uow.Questions.Get(model.Id.Value);
                if (question == null)
                {
                    return HttpNotFound();
                }

                oldValues = new { Text = question.Text, Type = question.Type, IsActive = question.IsActive, AllowMultipleChoices = question.AllowMultipleChoices };
                question.Text = model.Text;
                question.Type = model.Type;
                question.IsActive = model.IsActive;
                question.AllowMultipleChoices = string.Equals(model.Type, "Choice", StringComparison.OrdinalIgnoreCase) && model.AllowMultipleChoices;
                _uow.Questions.Update(question);

                var questionId = question.Id;
                var oldOptions = _uow.Context.Set<QuestionOption>().Where(o => o.QuestionId == questionId);
                _uow.Context.Set<QuestionOption>().RemoveRange(oldOptions);
            }
            else
            {
                question = new Question
                {
                    Text = model.Text,
                    Type = model.Type,
                    IsActive = model.IsActive,
                    AllowMultipleChoices = string.Equals(model.Type, "Choice", StringComparison.OrdinalIgnoreCase) && model.AllowMultipleChoices
                };

                var companyId = _tenantService.GetCurrentUserCompanyId();
                if (companyId.HasValue)
                {
                    question.CompanyId = companyId.Value;
                }

                _uow.Questions.Add(question);
                _uow.Complete();
            }

            _uow.Complete();
            return null;
        }

        private List<object> SaveQuestionOptions(int questionId, IEnumerable<QuestionOptionVM> options)
        {
            var savedOptions = new List<object>();
            if (options == null)
            {
                _uow.Complete();
                return savedOptions;
            }

            foreach (var opt in options)
            {
                if (string.IsNullOrWhiteSpace(opt.Text))
                {
                    continue;
                }

                var newOpt = new QuestionOption
                {
                    QuestionId = questionId,
                    Text = opt.Text,
                    Points = opt.Points
                };
                _uow.Context.Set<QuestionOption>().Add(newOpt);
                savedOptions.Add(new { Text = opt.Text, Points = opt.Points });
            }

            _uow.Complete();
            return savedOptions;
        }

        private void LogQuestionSaved(Question question, object oldValues, List<object> options, bool isUpdate)
        {
            var newValues = new
            {
                Text = question.Text,
                Type = question.Type,
                IsActive = question.IsActive,
                Options = options
            };

            if (isUpdate)
            {
                _auditService.LogUpdate(User.Identity.Name, "Admin", question.Id.ToString(), oldValues, newValues);
            }
            else
            {
                _auditService.LogCreate(User.Identity.Name, "Admin", question.Id.ToString(), newValues);
            }
        }

        private ActionResult HandleAddToSampleQuestions(string questionsJson)
        {
            try
            {
                var questions = ParseQuestionPayload(questionsJson);
                var existingQuestionsQuery = _tenantService.ApplyTenantFilter(_uow.Questions.GetAll().AsQueryable());
                var analysis = AnalyzeQuestionDuplicates(questions, existingQuestionsQuery.ToList());

                if (analysis.Duplicates.Any())
                {
                    return Json(new
                    {
                        success = true,
                        requiresDecision = true,
                        duplicates = analysis.Duplicates,
                        newQuestions = analysis.NewQuestions,
                        message = string.Format("Found {0} potential duplicates. Please review before adding.", analysis.Duplicates.Count)
                    });
                }

                return Json(new
                {
                    success = true,
                    requiresDecision = false,
                    message = string.Format("All {0} questions are already in the question bank.", questions.Count)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding questions to sample: " + ex.Message });
            }
        }

        private List<Dictionary<string, object>> ParseQuestionPayload(string questionsJson)
        {
            var parsed = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(questionsJson);
            return parsed ?? new List<Dictionary<string, object>>();
        }

        private DuplicateAnalysis AnalyzeQuestionDuplicates(IEnumerable<Dictionary<string, object>> questions, List<Question> existingQuestions)
        {
            var analysis = new DuplicateAnalysis
            {
                Duplicates = new List<object>(),
                NewQuestions = new List<object>()
            };

            foreach (var question in questions)
            {
                var questionText = GetQuestionField(question, "text");
                var questionType = GetQuestionField(question, "type");
                var similarQuestion = FindSimilarQuestion(existingQuestions, questionText);

                if (similarQuestion != null)
                {
                    analysis.Duplicates.Add(new
                    {
                        id = question.ContainsKey("id") ? question["id"] : null,
                        text = questionText,
                        type = questionType,
                        existingQuestionId = similarQuestion.Id,
                        existingQuestionText = similarQuestion.Text,
                        existingQuestionType = similarQuestion.Type
                    });
                    continue;
                }

                analysis.NewQuestions.Add(new { questionData = question });
            }

            return analysis;
        }

        private static string GetQuestionField(Dictionary<string, object> question, string key)
        {
            return question.ContainsKey(key) && question[key] != null ? question[key].ToString() : string.Empty;
        }

        private static Question FindSimilarQuestion(IEnumerable<Question> existingQuestions, string questionText)
        {
            if (string.IsNullOrWhiteSpace(questionText))
            {
                return null;
            }

            var comparisonText = questionText.ToLower();
            foreach (var existingQuestion in existingQuestions)
            {
                if (string.IsNullOrWhiteSpace(existingQuestion.Text))
                {
                    continue;
                }

                var existingText = existingQuestion.Text.ToLower();
                var questionPrefix = comparisonText.Substring(0, Math.Min(50, comparisonText.Length));
                var existingPrefix = existingText.Substring(0, Math.Min(50, existingText.Length));
                if (existingText.Contains(questionPrefix) || comparisonText.Contains(existingPrefix))
                {
                    return existingQuestion;
                }
            }

            return null;
        }

        private ActionResult HandleCreateUser(CreateUserViewModel model)
        {
            if (!_tenantService.IsActualSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            if (!ModelState.IsValid)
            {
                return ReturnCreateUserView(model);
            }

            var roleSelection = ResolveRoleSelection(model.SelectedRoleKey, true, null, model.CompanyId);
            if (!roleSelection.IsValid)
            {
                ModelState.AddModelError("SelectedRoleKey", roleSelection.ErrorMessage);
                return ReturnCreateUserView(model);
            }

            model.Role = roleSelection.BaseRole;
            if (!ValidateCreateUserUniqueness(model))
            {
                return ReturnCreateUserView(model);
            }

            try
            {
                var user = BuildUserFromCreateModel(model, roleSelection);
                _uow.Users.Add(user);
                _uow.Complete();

                CreateApplicantForClientRole(model, user.CompanyId);
                LogCreatedUser(user);

                TempData["SuccessMessage"] = string.Format("User {0} has been created successfully.", user.UserName);
                return RedirectToAction("GlobalUserManagement");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                return ReturnCreateUserView(model);
            }
        }

        private ActionResult ReturnCreateUserView(CreateUserViewModel model)
        {
            model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
            model.AvailableRoleOptions = BuildAvailableRoleOptions(true, null, model.CompanyId, false, model.SelectedRoleKey);
            return View("CreateUser", model);
        }

        private bool ValidateCreateUserUniqueness(CreateUserViewModel model)
        {
            var existingUserInCompany = _uow.Users.GetAll().FirstOrDefault(u =>
                u.UserName.Equals(model.UserName, StringComparison.OrdinalIgnoreCase) &&
                u.CompanyId == model.CompanyId);
            if (existingUserInCompany != null)
            {
                ModelState.AddModelError("UserName", "Username already exists within this company.");
            }

            var existingEmail = _uow.Users.GetAll()
                .FirstOrDefault(u => u.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));
            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "Email already exists.");
            }

            return ModelState.IsValid;
        }

        private static User BuildUserFromCreateModel(CreateUserViewModel model, RoleSelectionResolution roleSelection)
        {
            return new User
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                UserName = model.UserName,
                Email = model.Email,
                Role = roleSelection.BaseRole,
                RoleDefinitionId = roleSelection.RoleDefinitionId,
                PasswordHash = PasswordHelper.HashPassword(model.Password),
                CompanyId = model.CompanyId,
                RequirePasswordChange = model.RequirePasswordChange
            };
        }

        private void CreateApplicantForClientRole(CreateUserViewModel model, int? companyId)
        {
            if (model.Role != "Client")
            {
                return;
            }

            var applicant = new Applicant
            {
                FullName = string.Format("{0} {1}", model.FirstName, model.LastName),
                Email = model.Email,
                Phone = model.Phone,
                CompanyId = companyId
            };
            _uow.Applicants.Add(applicant);
            _uow.Complete();
        }

        private void LogCreatedUser(User user)
        {
            var displayRole = _rolePermissionService.GetDisplayRole(user);
            _auditService.LogAction(
                User.Identity.Name,
                "USER_CREATED",
                "UserManagement",
                user.Id.ToString(),
                true,
                string.Format("Created user {0} ({1}) with role {2}", user.UserName, user.Email, displayRole)
            );
        }

        private class DuplicateAnalysis
        {
            public List<object> Duplicates { get; set; }
            public List<object> NewQuestions { get; set; }
        }
    }
}
