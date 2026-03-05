using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public partial class AdminController : Controller
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
        public async Task<ActionResult> GenerateQuestions(string jobTitle, string jobDescription, string keyResponsibilities, string requiredQualifications, int count, string experience = "mid", string[] questionTypes = null)
        {
            try
            {
                // Debug: Log to System.Diagnostics instead of Response
                var debugMsg = string.Format("=== GenerateQuestions Debug ===\njobTitle: {0}\njobDescription length: {1}\ncount: {2}\nexperience: {3}\nquestionTypes: {4}\n",
                    jobTitle, jobDescription != null ? jobDescription.Length : 0, count, experience, questionTypes != null ? string.Join(", ", questionTypes) : "null");
                
                System.Diagnostics.Debug.WriteLine(debugMsg);
                System.Diagnostics.Trace.WriteLine(debugMsg);

                // Validate input
                if (string.IsNullOrEmpty(jobTitle) || string.IsNullOrEmpty(jobDescription))
                {
                    var errorMsg = "Job title and description are required";
                    System.Diagnostics.Debug.WriteLine("Validation Error: " + errorMsg);
                    return Json(new { success = false, message = errorMsg });
                }

                if (count <= 0)
                {
                    var errorMsg = "Number of questions must be greater than 0";
                    System.Diagnostics.Debug.WriteLine("Validation Error: " + errorMsg);
                    return Json(new { success = false, message = errorMsg });
                }

                if (count > 50)
                {
                    var errorMsg = "Number of questions cannot exceed 50";
                System.Diagnostics.Debug.WriteLine(string.Format("Validation Error: {0}", errorMsg));
                    return Json(new { success = false, message = errorMsg });
                }

                System.Diagnostics.Debug.WriteLine("Input validation passed");

                // Generate questions dynamically
                var result = _questionService.GenerateQuestions(
                    jobTitle: jobTitle,
                    jobDescription: jobDescription,
                    keyResponsibilities: keyResponsibilities ?? "",
                    requiredQualifications: requiredQualifications ?? "",
                    experience: experience,
                    questionCount: count,
                    questionTypes: questionTypes != null ? questionTypes.ToList() : null
                );

                System.Diagnostics.Debug.WriteLine(string.Format("Question service returned: Success={0}, QuestionCount={1}", result.Success, result.Questions != null ? result.Questions.Count : 0));

                if (result.Success)
                {
                    // Convert to the expected format
                    var questions = result.Questions.Select(q => new
                    {
                        id = q.Id,
                        text = q.Text,
                        type = q.Type,
                        category = q.Category,
                        difficulty = q.Difficulty,
                        maxPoints = q.MaxPoints,
                        suggestedOptions = q.Options.Select(o => new
                        {
                            text = o.Text,
                            points = o.Points
                        }).ToList()
                    }).ToList();

                    return Json(new
                    {
                        success = true,
                        loading = false,
                        questions = questions,
                        metadata = result.Metadata
                    }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        error = result.Message,
                        loading = false
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error generating questions: " + ex.Message });
            }
        }

        /// <summary>
        /// Check status of question generation and retrieve results (for compatibility with existing frontend)
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> CheckQuestionStatus()
        {
            try
            {
                // Get parameters from Request.Form for compatibility
                var jobTitle = Request.Form["jobTitle"];
                var jobDescription = Request.Form["jobDescription"];
                var keyResponsibilities = Request.Form["keyResponsibilities"];
                var requiredQualifications = Request.Form["requiredQualifications"];
                var experience = Request.Form["experience"] ?? "mid";
                var count = int.Parse(Request.Form["count"] ?? "5");
                
                // Handle questionTypes array
                var questionTypes = Request.Form.GetValues("questionTypes");

                // Generate questions directly (no background processing needed)
                var result = _questionService.GenerateQuestions(
                    jobTitle: jobTitle,
                    jobDescription: jobDescription,
                    keyResponsibilities: keyResponsibilities ?? "",
                    requiredQualifications: requiredQualifications ?? "",
                    experience: experience,
                    questionCount: count,
                    questionTypes: questionTypes != null ? questionTypes.ToList() : null
                );

                if (result.Success)
                {
                    // Convert to the expected format
                    var questions = result.Questions.Select(q => new
                    {
                        id = q.Id,
                        text = q.Text,
                        type = q.Type,
                        category = q.Category,
                        difficulty = q.Difficulty,
                        maxPoints = q.MaxPoints,
                        suggestedOptions = q.Options.Select(o => new
                        {
                            text = o.Text,
                            points = o.Points
                        }).ToList()
                    }).ToList();

                    return Json(new
                    {
                        success = true,
                        loading = false,
                        questions = questions,
                        metadata = result.Metadata
                    }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        error = result.Message,
                        loading = false
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Check for duplicate questions in sample collection
        /// </summary>
        [HttpPost]
        public ActionResult CheckDuplicateQuestions(string questionsJson)
        {
            try
            {
                var questionsToAdd = JsonConvert.DeserializeObject<List<GeneratedQuestion>>(questionsJson);
                var duplicates = new List<object>();
                var newQuestions = new List<object>();

                // Get all existing sample questions
                var existingQuestions = _uow.Context.Set<Question>()
                    .Where(q => q.IsActive)
                    .ToList();

                foreach (var question in questionsToAdd)
                {
                    var questionText = question.Text ?? "";
                    var questionType = question.Type ?? "Text";

                    // Check for similar questions (simple text similarity)
                    var similarQuestion = existingQuestions.FirstOrDefault(eq => 
                        eq.Text.ToLower().Contains(questionText.ToLower().Substring(0, Math.Min(50, questionText.Length))) ||
                        questionText.ToLower().Contains(eq.Text.ToLower().Substring(0, Math.Min(50, eq.Text.Length))));

                    if (similarQuestion != null)
                    {
                        duplicates.Add(new
                        {
                            id = question.Id,
                            text = questionText,
                            type = questionType,
                            existingQuestionId = similarQuestion.Id,
                            existingQuestionText = similarQuestion.Text,
                            existingQuestionType = similarQuestion.Type
                        });
                    }
                    else
                    {
                        newQuestions.Add(new
                        {
                            id = question.Id,
                            text = questionText,
                            type = questionType
                        });
                    }
                }

                return Json(new { 
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

        /// <summary>
        /// Add generated questions to sample questions collection
        /// </summary>
        [HttpPost]
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

        /// <summary>
        /// Process duplicate decisions and add approved questions
        /// </summary>
        [HttpPost]
        public ActionResult ProcessDuplicateDecisions(string decisionsJson)
        {
            try
            {
                var decisions = JsonConvert.DeserializeObject<List<dynamic>>(decisionsJson);
                int addedCount = 0;

                foreach (var decision in decisions)
                {
                    if (decision.action.ToString() == "keep")
                    {
                        // Create the question even if it's similar to existing one
                        var question = new Question
                        {
                            Text = decision.newText.ToString(),
                            Type = decision.type.ToString(),
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
        public ActionResult CreatePositionWithQuestions(string positionTitle, string positionDescription, 
            string positionDepartment, string positionSalaryMin, string positionSalaryMax,
            string positionKeyResponsibilities, string positionRequiredQualifications, 
            string questionsJson)
        {
            try
            {
                var questions = JsonConvert.DeserializeObject<List<GeneratedQuestion>>(questionsJson);
                var currentCompanyId = _tenantService.GetCurrentUserCompanyId();
                
                // Debug: Log to System.Diagnostics
                System.Diagnostics.Debug.WriteLine("=== CreatePositionWithQuestions Debug ===");
                
                // Fix: Handle suggestedOptions property name mismatch
                var questionsJsonFixed = questionsJson.Replace("\"suggestedOptions\"", "\"Options\"");
                var questionsFixed = JsonConvert.DeserializeObject<List<GeneratedQuestion>>(questionsJsonFixed);
                
                // Find the department by name AND company
                var department = _uow.Context.Set<Department>()
                    .FirstOrDefault(d => d.Name == positionDepartment && d.CompanyId == currentCompanyId);
                
                if (department == null)
                {
                    return Json(new { success = false, message = "Department not found: " + positionDepartment });
                }
                
                // Create the position
                var position = new Position
                {
                    Title = positionTitle,
                    Description = positionDescription,
                    Responsibilities = positionKeyResponsibilities,
                    Qualifications = positionRequiredQualifications,
                    DepartmentId = department.Id,
                    PostedOn = DateTime.UtcNow,
                    IsOpen = true,
                    Currency = "KES",
                    SalaryMin = string.IsNullOrEmpty(positionSalaryMin) ? (int?)null : int.Parse(positionSalaryMin),
                    SalaryMax = string.IsNullOrEmpty(positionSalaryMax) ? (int?)null : int.Parse(positionSalaryMax),
                    CompanyId = currentCompanyId
                };
                
                _uow.Context.Set<Position>().Add(position);
                _uow.Complete();
                
                // Add questions to the position
                foreach (var generatedQ in questionsFixed)
                {
                    // First, create the question in the main question bank
                    var question = new Question
                    {
                        Text = generatedQ.Text,
                        Type = generatedQ.Type,
                        IsActive = true,
                        CompanyId = currentCompanyId
                    };
                    _uow.Context.Set<Question>().Add(question);
                    _uow.Complete(); // Save question first to get ID
                    
                    // Create question options if they exist
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
                    
                    // Assign the question to the position
                    var positionQuestion = new PositionQuestion
                    {
                        PositionId = position.Id,
                        QuestionId = question.Id,
                        IsRequired = true // Default to required for generated questions
                    };
                    _uow.Context.Set<PositionQuestion>().Add(positionQuestion);
                    _uow.Complete(); // Save assignment
                }
                
                return Json(new { 
                    success = true, 
                    message = string.Format("Successfully created position '{0}' with {1} questions assigned.", positionTitle, questionsFixed.Count),
                    positionId = position.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error creating position: " + ex.Message });
            }
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
