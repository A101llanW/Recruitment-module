using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.ViewModels;
using Newtonsoft.Json;
using GeneratedQuestion = HR.Web.Services.DynamicQuestionService.GeneratedQuestion;

namespace HR.Web.Controllers
{
    /// <summary>
    /// Dynamic Question Generation Controller - Replaces MCP server
    /// </summary>
    public partial class AdminController
    {
        private readonly DynamicQuestionService _questionService = new DynamicQuestionService();

        /// <summary>
        /// Generate questions using dynamic analysis based on job description
        /// </summary>
        // GET: Admin/GenerateQuestions
        [HttpGet]
        public ActionResult GenerateQuestions()
        {
            return View(new GenerateQuestionsViewModel());
        }


        // POST: Admin/GenerateQuestions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateQuestions(string jobTitle, string jobDescription, string keyResponsibilities, string requiredQualifications, int count, string experience, string[] questionTypes)
        {
            return HandleGenerateQuestions(jobTitle, jobDescription, keyResponsibilities, requiredQualifications, count, experience, questionTypes);
        }

        /// <summary>
        /// Check status of question generation and retrieve results (for compatibility with existing frontend)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckQuestionStatus()
        {
            return HandleCheckQuestionStatus();
        }

        /// <summary>
        /// Check for duplicate questions in sample collection
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckDuplicateQuestions(string questionsJson)
        {
            return HandleCheckDuplicateQuestions(questionsJson);
        }

        /// <summary>
        /// Add generated questions to sample questions collection
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddGeneratedQuestionsToSample(string questionsJson)
        {
            try
            {
                // Fix: Handle suggestedOptions property name mismatch
                var questionsJsonFixed = questionsJson.Replace("\"suggestedOptions\"", "\"Options\"");
                var questions = JsonConvert.DeserializeObject<List<GeneratedQuestion>>(questionsJsonFixed);
                int addedCount = 0;

                foreach (var generatedQ in questions)
                {
                    // Create the main question with available properties only
                    var question = new Question
                    {
                        Text = generatedQ.Text,
                        Type = generatedQ.Type,
                        IsActive = true,
                        CompanyId = _tenantService.GetCurrentUserCompanyId()
                    };
                    
                    _uow.Questions.Add(question);
                    _uow.Complete();
                    
                    // Create question options for choice/number/rating questions
                    if (generatedQ.Options != null && generatedQ.Options.Any())
                    {
                        foreach (var option in generatedQ.Options)
                        {
                            var questionOption = new QuestionOption
                            {
                                QuestionId = question.Id,
                                Text = option.Text,
                                Points = option.Points
                            };
                            
                            _uow.Context.Set<QuestionOption>().Add(questionOption);
                        }
                        
                        _uow.Complete(); // Save options
                    }

                    addedCount++;
                }

                return Json(new { 
                    success = true, 
                    message = string.Format("Successfully added {0} questions to the sample questions collection.", addedCount),
                    count = addedCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding questions to sample collection: " + ex.Message });
            }
        }

        /// <summary>
        /// Add questions to sample questions collection (direct addition without duplicate checking)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddQuestionsToSample(string questionsJson)
        {
            try
            {
                // Fix: Handle suggestedOptions property name mismatch
                var questionsJsonFixed = questionsJson.Replace("\"suggestedOptions\"", "\"Options\"");
                var questions = JsonConvert.DeserializeObject<List<GeneratedQuestion>>(questionsJsonFixed);
                int addedCount = 0;

                foreach (var generatedQ in questions)
                {
                    // Create the main question with available properties only
                    var question = new Question
                    {
                        Text = generatedQ.Text,
                        Type = generatedQ.Type,
                        IsActive = true,
                        CompanyId = _tenantService.GetCurrentUserCompanyId()
                    };
                    
                    _uow.Questions.Add(question);
                    _uow.Complete();
                    
                    // Create question options for choice/number/rating questions
                    if (generatedQ.Options != null && generatedQ.Options.Any())
                    {
                        foreach (var option in generatedQ.Options)
                        {
                            var questionOption = new QuestionOption
                            {
                                QuestionId = question.Id,
                                Text = option.Text,
                                Points = option.Points
                            };
                            
                            _uow.Context.Set<QuestionOption>().Add(questionOption);
                        }
                        
                        _uow.Complete(); // Save options
                    }

                    addedCount++;
                }

                return Json(new { 
                    success = true, 
                    message = string.Format("Successfully added {0} questions to the sample questions collection.", addedCount),
                    count = addedCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding questions to sample collection: " + ex.Message });
            }
        }

        private sealed class DuplicateDecisionDto
        {
            public string action { get; set; }
            public string newText { get; set; }
            public string type { get; set; }
        }

        /// <summary>
        /// Process duplicate decisions and add approved questions
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessDuplicateDecisions(string decisionsJson)
        {
            try
            {
                var decisions = JsonConvert.DeserializeObject<List<DuplicateDecisionDto>>(decisionsJson) ?? new List<DuplicateDecisionDto>();
                int addedCount = 0;

                foreach (var decision in decisions)
                {
                    if (decision != null && string.Equals(decision.action, "keep", StringComparison.OrdinalIgnoreCase))
                    {
                        // Create the question even if it's similar to existing one
                        var question = new Question
                        {
                            Text = decision.newText,
                            Type = decision.type,
                            IsActive = true,
                            CompanyId = _tenantService.GetCurrentUserCompanyId()
                        };
                        _uow.Context.Set<Question>().Add(question);
                        _uow.Complete();
                        addedCount++;
                    }
                    // If action is "skip", we don't add the question
                }

                return Json(new { 
                    success = true, 
                    message = string.Format("Successfully processed decisions. Added {0} new questions to the sample collection.", addedCount),
                    addedCount = addedCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error processing duplicate decisions: " + ex.Message });
            }
        }

        /// <summary>
        /// Create a new position with assigned questions
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePositionWithQuestions(string positionTitle, string positionDescription, 
            string positionDepartment, string positionSalaryMin, string positionSalaryMax,
            string positionKeyResponsibilities, string positionRequiredQualifications, 
            string questionsJson)
        {
            return HandleCreatePositionWithQuestions(positionTitle, positionDescription, positionDepartment, positionSalaryMin, positionSalaryMax, positionKeyResponsibilities, positionRequiredQualifications, questionsJson);
        }

        /// <summary>
        /// Get departments for dropdown
        /// </summary>
        [HttpGet]
        public ActionResult GetDepartments()
        {
            try
            {
                var companyId = _tenantService.GetCurrentUserCompanyId();
                var departmentsQuery = _uow.Context.Set<Department>().AsQueryable();
                
                if (companyId.HasValue)
                {
                    departmentsQuery = departmentsQuery.Where(d => d.CompanyId == companyId.Value);
                }

                var departments = departmentsQuery
                    .OrderBy(d => d.Name)
                    .Select(d => d.Name)
                    .ToList();

                return Json(new { success = true, departments = departments }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Test connection to question service (for compatibility)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddGeneratedQuestionsToBank(string questionsJson)
        {
            try
            {
                // Fix: Handle suggestedOptions property name mismatch
                var questionsJsonFixed = questionsJson.Replace("\"suggestedOptions\"", "\"Options\"");
                var questions = JsonConvert.DeserializeObject<List<GeneratedQuestion>>(questionsJsonFixed);
                
                foreach (var generatedQ in questions)
                {
                    // Create the main question with available properties only
                    var question = new Question
                    {
                        Text = generatedQ.Text,
                        Type = generatedQ.Type,
                        IsActive = true,
                        CompanyId = _tenantService.GetCurrentUserCompanyId()
                    };
                    
                    _uow.Questions.Add(question);
                    _uow.Complete();
                    
                    // Create question options for choice/number/rating questions
                    if (generatedQ.Options != null && generatedQ.Options.Any())
                    {
                        foreach (var option in generatedQ.Options)
                        {
                            var questionOption = new QuestionOption
                            {
                                QuestionId = question.Id,
                                Text = option.Text,
                                Points = option.Points
                            };
                            
                            _uow.Context.Set<QuestionOption>().Add(questionOption);
                        }
                        
                        _uow.Complete();
                    }
                }
                
                return Json(new { success = true, message = string.Format("Successfully added {0} questions to the question bank.", questions.Count) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding questions to bank: " + ex.Message });
            }
        }


    }
}
