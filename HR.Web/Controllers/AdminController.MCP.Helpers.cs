using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Models;
using HR.Web.Services;
using Newtonsoft.Json;
using GeneratedQuestion = HR.Web.Services.DynamicQuestionService.GeneratedQuestion;
using GeneratedQuestionOption = HR.Web.Services.DynamicQuestionService.QuestionOption;
using QuestionGenerationResult = HR.Web.Services.DynamicQuestionService.QuestionGenerationResult;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        private const int MaxGeneratedQuestionCount = 50;
        private const int DefaultGeneratedQuestionCount = 5;

        private sealed class QuestionGenerationRequestModel
        {
            public string JobTitle { get; set; }
            public string JobDescription { get; set; }
            public string KeyResponsibilities { get; set; }
            public string RequiredQualifications { get; set; }
            public string Experience { get; set; }
            public int Count { get; set; }
            public List<string> QuestionTypes { get; set; }
        }

        private ActionResult HandleGenerateQuestions(
            string jobTitle,
            string jobDescription,
            string keyResponsibilities,
            string requiredQualifications,
            int count,
            string experience,
            string[] questionTypes)
        {
            try
            {
                var request = BuildQuestionGenerationRequest(jobTitle, jobDescription, keyResponsibilities, requiredQualifications, count, experience, questionTypes);
                LogQuestionGenerationRequest(request);

                var validationError = ValidateQuestionGenerationRequest(request);
                if (validationError != null)
                {
                    System.Diagnostics.Debug.WriteLine("Validation Error: " + validationError);
                    return Json(new { success = false, message = validationError });
                }

                System.Diagnostics.Debug.WriteLine("Input validation passed");
                var result = GenerateQuestions(request);
                return BuildQuestionGenerationResponse(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error generating questions: " + ex.Message });
            }
        }

        private ActionResult HandleCheckQuestionStatus()
        {
            try
            {
                var request = BuildQuestionGenerationRequest(Request.Form);
                var validationError = ValidateQuestionGenerationRequest(request);
                if (validationError != null)
                {
                    return Json(new { success = false, message = validationError });
                }

                var result = GenerateQuestions(request);
                return BuildQuestionGenerationResponse(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private static QuestionGenerationRequestModel BuildQuestionGenerationRequest(
            string jobTitle,
            string jobDescription,
            string keyResponsibilities,
            string requiredQualifications,
            int count,
            string experience,
            string[] questionTypes)
        {
            return new QuestionGenerationRequestModel
            {
                JobTitle = jobTitle,
                JobDescription = jobDescription,
                KeyResponsibilities = keyResponsibilities ?? string.Empty,
                RequiredQualifications = requiredQualifications ?? string.Empty,
                Experience = NormalizeExperience(experience),
                Count = count,
                QuestionTypes = questionTypes != null ? questionTypes.ToList() : null
            };
        }

        private static QuestionGenerationRequestModel BuildQuestionGenerationRequest(NameValueCollection form)
        {
            int count;
            if (!int.TryParse(form["count"], out count))
            {
                count = DefaultGeneratedQuestionCount;
            }

            return BuildQuestionGenerationRequest(
                form["jobTitle"],
                form["jobDescription"],
                form["keyResponsibilities"],
                form["requiredQualifications"],
                count,
                form["experience"],
                form.GetValues("questionTypes"));
        }

        private static string NormalizeExperience(string experience)
        {
            return string.IsNullOrWhiteSpace(experience) ? "mid" : experience;
        }

        private static string ValidateQuestionGenerationRequest(QuestionGenerationRequestModel request)
        {
            if (string.IsNullOrWhiteSpace(request.JobTitle) || string.IsNullOrWhiteSpace(request.JobDescription))
            {
                return "Job title and description are required";
            }

            if (request.Count <= 0)
            {
                return "Number of questions must be greater than 0";
            }

            if (request.Count > MaxGeneratedQuestionCount)
            {
                return "Number of questions cannot exceed 50";
            }

            return null;
        }

        private static void LogQuestionGenerationRequest(QuestionGenerationRequestModel request)
        {
            var debugMsg = string.Format(
                "=== GenerateQuestions Debug ===\njobTitle: {0}\njobDescription length: {1}\ncount: {2}\nexperience: {3}\nquestionTypes: {4}\n",
                request.JobTitle,
                request.JobDescription != null ? request.JobDescription.Length : 0,
                request.Count,
                request.Experience,
                request.QuestionTypes != null ? string.Join(", ", request.QuestionTypes) : "null");

            System.Diagnostics.Debug.WriteLine(debugMsg);
            System.Diagnostics.Trace.WriteLine(debugMsg);
        }

        private QuestionGenerationResult GenerateQuestions(QuestionGenerationRequestModel request)
        {
            var result = _questionService.GenerateQuestions(
                jobTitle: request.JobTitle,
                jobDescription: request.JobDescription,
                keyResponsibilities: request.KeyResponsibilities,
                requiredQualifications: request.RequiredQualifications,
                experience: request.Experience,
                questionCount: request.Count,
                questionTypes: request.QuestionTypes);

            System.Diagnostics.Debug.WriteLine(
                string.Format("Question service returned: Success={0}, QuestionCount={1}", result.Success, result.Questions != null ? result.Questions.Count : 0));

            return result;
        }

        private ActionResult BuildQuestionGenerationResponse(QuestionGenerationResult result)
        {
            if (result.Success)
            {
                return Json(
                    new
                    {
                        success = true,
                        loading = false,
                        questions = MapGeneratedQuestions(result.Questions),
                        metadata = result.Metadata
                    },
                    JsonRequestBehavior.AllowGet);
            }

            return Json(
                new
                {
                    success = false,
                    error = result.Message,
                    loading = false
                },
                JsonRequestBehavior.AllowGet);
        }

        private static List<object> MapGeneratedQuestions(IEnumerable<GeneratedQuestion> questions)
        {
            var generatedQuestions = questions ?? Enumerable.Empty<GeneratedQuestion>();
            return generatedQuestions.Select(MapGeneratedQuestion).ToList();
        }

        private static object MapGeneratedQuestion(GeneratedQuestion question)
        {
            return new
            {
                id = question.Id,
                text = question.Text,
                type = question.Type,
                category = question.Category,
                difficulty = question.Difficulty,
                maxPoints = question.MaxPoints,
                suggestedOptions = MapGeneratedQuestionOptions(question.Options)
            };
        }

        private static List<object> MapGeneratedQuestionOptions(IEnumerable<GeneratedQuestionOption> options)
        {
            var questionOptions = options ?? Enumerable.Empty<GeneratedQuestionOption>();
            return questionOptions.Select(o => new { text = o.Text, points = o.Points }).Cast<object>().ToList();
        }

        private ActionResult HandleCheckDuplicateQuestions(string questionsJson)
        {
            try
            {
                var questionsToAdd = DeserializeGeneratedQuestions(questionsJson);
                var existingQuestions = GetActiveQuestions();
                var duplicates = new List<object>();
                var newQuestions = new List<object>();

                foreach (var question in questionsToAdd)
                {
                    CategorizeQuestion(existingQuestions, question, duplicates, newQuestions);
                }

                return Json(
                    new
                    {
                        success = true,
                        duplicates = duplicates,
                        newQuestions = newQuestions,
                        totalDuplicates = duplicates.Count,
                        totalNew = newQuestions.Count
                    });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private List<Question> GetActiveQuestions()
        {
            return _uow.Context.Set<Question>()
                .Where(q => q.IsActive)
                .ToList();
        }

        private static void CategorizeQuestion(
            IEnumerable<Question> existingQuestions,
            GeneratedQuestion question,
            ICollection<object> duplicates,
            ICollection<object> newQuestions)
        {
            var questionText = question.Text ?? string.Empty;
            var questionType = question.Type ?? "Text";
            var similarQuestion = FindSimilarGeneratedQuestion(existingQuestions, questionText);

            if (similarQuestion != null)
            {
                duplicates.Add(
                    new
                    {
                        id = question.Id,
                        text = questionText,
                        type = questionType,
                        existingQuestionId = similarQuestion.Id,
                        existingQuestionText = similarQuestion.Text,
                        existingQuestionType = similarQuestion.Type
                    });
                return;
            }

            newQuestions.Add(
                new
                {
                    id = question.Id,
                    text = questionText,
                    type = questionType
                });
        }

        private static Question FindSimilarGeneratedQuestion(IEnumerable<Question> existingQuestions, string candidateText)
        {
            var normalizedCandidate = (candidateText ?? string.Empty).ToLower();
            var candidatePrefix = BuildComparisonPrefix(normalizedCandidate);

            return existingQuestions.FirstOrDefault(existing =>
            {
                var existingText = (existing.Text ?? string.Empty).ToLower();
                var existingPrefix = BuildComparisonPrefix(existingText);
                return existingText.Contains(candidatePrefix) || normalizedCandidate.Contains(existingPrefix);
            });
        }

        private static string BuildComparisonPrefix(string text)
        {
            var normalizedText = text ?? string.Empty;
            return normalizedText.Substring(0, Math.Min(50, normalizedText.Length));
        }

        private ActionResult HandleCreatePositionWithQuestions(
            string positionTitle,
            string positionDescription,
            string positionDepartment,
            string positionSalaryMin,
            string positionSalaryMax,
            string positionKeyResponsibilities,
            string positionRequiredQualifications,
            string questionsJson)
        {
            try
            {
                var currentCompanyId = _tenantService.GetCurrentUserCompanyId();
                System.Diagnostics.Debug.WriteLine("=== CreatePositionWithQuestions Debug ===");

                var generatedQuestions = DeserializeGeneratedQuestions(questionsJson);
                var department = FindDepartment(positionDepartment, currentCompanyId);
                if (department == null)
                {
                    return Json(new { success = false, message = "Department not found: " + positionDepartment });
                }

                var position = BuildPosition(
                    positionTitle,
                    positionDescription,
                    positionKeyResponsibilities,
                    positionRequiredQualifications,
                    positionSalaryMin,
                    positionSalaryMax,
                    department.Id,
                    currentCompanyId);

                _uow.Context.Set<Position>().Add(position);
                _uow.Complete();

                AddGeneratedQuestionsToPosition(position.Id, generatedQuestions, currentCompanyId);

                return Json(
                    new
                    {
                        success = true,
                        message = string.Format(
                            "Successfully created position '{0}' with {1} questions assigned.",
                            positionTitle,
                            generatedQuestions.Count),
                        positionId = position.Id
                    });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error creating position: " + ex.Message });
            }
        }

        private static List<GeneratedQuestion> DeserializeGeneratedQuestions(string questionsJson)
        {
            if (questionsJson == null)
            {
                throw new ArgumentNullException("questionsJson");
            }

            var questionsJsonFixed = questionsJson.Replace("\"suggestedOptions\"", "\"Options\"");
            return JsonConvert.DeserializeObject<List<GeneratedQuestion>>(questionsJsonFixed) ?? new List<GeneratedQuestion>();
        }

        private Department FindDepartment(string departmentName, int? companyId)
        {
            return _uow.Context.Set<Department>()
                .FirstOrDefault(d => d.Name == departmentName && d.CompanyId == companyId);
        }

        private static Position BuildPosition(
            string title,
            string description,
            string responsibilities,
            string qualifications,
            string salaryMin,
            string salaryMax,
            int departmentId,
            int? companyId)
        {
            return new Position
            {
                Title = title,
                Description = description,
                Responsibilities = responsibilities,
                Qualifications = qualifications,
                DepartmentId = departmentId,
                PostedOn = DateTime.UtcNow,
                IsOpen = true,
                Currency = "KES",
                SalaryMin = ParseNullableInt(salaryMin),
                SalaryMax = ParseNullableInt(salaryMax),
                CompanyId = companyId
            };
        }

        private static int? ParseNullableInt(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return int.Parse(value);
        }

        private void AddGeneratedQuestionsToPosition(int positionId, IEnumerable<GeneratedQuestion> generatedQuestions, int? companyId)
        {
            foreach (var generatedQuestion in generatedQuestions)
            {
                var questionId = CreateQuestionForCompany(generatedQuestion, companyId);
                AddQuestionOptions(questionId, generatedQuestion.Options);
                LinkQuestionToPosition(positionId, questionId);
            }
        }

        private int CreateQuestionForCompany(GeneratedQuestion generatedQuestion, int? companyId)
        {
            var question = new Question
            {
                Text = generatedQuestion.Text,
                Type = generatedQuestion.Type,
                IsActive = true,
                CompanyId = companyId
            };

            _uow.Context.Set<Question>().Add(question);
            _uow.Complete();
            return question.Id;
        }

        private void AddQuestionOptions(int questionId, IEnumerable<GeneratedQuestionOption> options)
        {
            if (options == null || !options.Any())
            {
                return;
            }

            foreach (var option in options)
            {
                _uow.Context.Set<QuestionOption>().Add(
                    new QuestionOption
                    {
                        QuestionId = questionId,
                        Text = option.Text,
                        Points = option.Points
                    });
            }

            _uow.Complete();
        }

        private void LinkQuestionToPosition(int positionId, int questionId)
        {
            _uow.Context.Set<PositionQuestion>().Add(
                new PositionQuestion
                {
                    PositionId = positionId,
                    QuestionId = questionId,
                    IsRequired = true
                });
            _uow.Complete();
        }
    }
}
