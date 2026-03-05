using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.ViewModels;
using HR.Web.Helpers;
using HR.Web.Filters;
using Newtonsoft.Json;

namespace HR.Web.Controllers
{
    /// <summary>
    /// Admin controller for managing candidates, applications, and rankings
    /// Allows admins to view candidates ranked by position, filter, and manage applications
    /// </summary>
    [Authorize(Roles = "Admin, SuperAdmin")]
    [RoleBasedAuthorization("Admin", "SuperAdmin")]
    public partial class AdminController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly SecurityService _securityService = new SecurityService();
        private readonly AuditService _auditService = new AuditService();
        private readonly TenantService _tenantService = new TenantService();

        // GET: Admin/Index - Default admin dashboard
        public ActionResult Index()
        {
            if (_tenantService.IsActualSuperAdmin() || User.IsInRole("SuperAdmin"))
            {
                return RedirectToAction("GlobalUserManagement");
            }

            // Redirect to user management as the default admin page
            return RedirectToAction("UserManagement");
        }

        /// <summary>
        /// Display candidates ranked by position with filtering capability
        /// 
        /// Query Parameters:
        /// - positionId (optional): Filter candidates by specific position ID
        /// 
        /// Features:
        /// - Groups candidates by position they applied for
        /// - Ranks candidates within each position by total score
        /// - Shows quick stats (applicant count per position)
        /// - Expandable details for each candidate
        /// - Action links to view details and schedule interviews
        /// </summary>
        public ActionResult CandidateRankings(int? positionId)
        {
            // Get all applications with related data
            var applicationsQuery = _uow.Applications.GetAll(
                a => a.Applicant,
                a => a.Position,
                a => a.Position.Department
            ).AsQueryable();

            // Apply tenant filter
            applicationsQuery = _tenantService.ApplyTenantFilter(applicationsQuery);

            var applications = applicationsQuery.ToList();

            // Filter by position if specified
            if (positionId.HasValue)
            {
                applications = applications.Where(a => a.PositionId == positionId.Value).ToList();
            }

            // Group applications by position
            var candidatesByPosition = new Dictionary<Position, List<CandidateApplicationScore>>();
            
            foreach (var application in applications)
            {
                if (application.Position == null) continue;

                // Always calculate a fresh questionnaire-based score so candidates
                // are differentiated by their actual answers
                var questionnaireScore = 0m;
                try
                {
                    questionnaireScore = _scoringService.CalculateApplicationScore(application);
                }
                catch
                {
                    // If scoring fails for any reason, fall back to stored score
                    questionnaireScore = application.Score ?? 0;
                }

                if (!candidatesByPosition.ContainsKey(application.Position))
                {
                    candidatesByPosition[application.Position] = new List<CandidateApplicationScore>();
                }

                var candidateScore = new CandidateApplicationScore
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

                candidatesByPosition[application.Position].Add(candidateScore);
            }

            // Sort candidates within each position by score (descending)
            foreach (var position in candidatesByPosition.Keys.ToList())
            {
                candidatesByPosition[position] = candidatesByPosition[position]
                    .OrderByDescending(c => c.TotalScore)
                    .ToList();
            }

            // Get all positions for the filter dropdown
            var allPositions = _uow.Positions.GetAll(p => p.Department).ToList();

            var viewModel = new CandidateRankingsViewModel
            {
                Positions = allPositions,
                CandidatesByPosition = candidatesByPosition
            };

            ViewBag.SelectedPositionId = positionId;
            return View(viewModel);
        }

        /// <summary>
        /// Helper method to calculate total score for a candidate
        /// 
        /// Scoring Logic:
        /// - Questionnaire responses weighted by importance
        /// - Any additional scoring factors (e.g., resume score, experience match)
        /// - Returns normalized score (typically 0-100)
        /// </summary>
        private decimal CalculateTotalScore(dynamic application)
        {
            // Placeholder method - scoring logic to be implemented
            return 0;
        }

        /// <summary>
        /// Display detailed information about a candidate's application
        /// Used when admin clicks "View Details" from rankings view
        /// </summary>
        public ActionResult ViewApplicationDetails(int applicationId)
        {
            var application = _uow.Applications.Get(applicationId);
            if (application == null)
            {
                return HttpNotFound();
            }
            
            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && application.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied: This application belongs to another company.");
            }
            
            // Get score breakdown and related data
            var breakdown = _scoringService.GetScoreBreakdown(applicationId);
            var maxScore = _scoringService.GetMaxScoreForPosition(application.PositionId);
            
            // Get all applications for ranking calculation
            var applicationsQuery = _uow.Applications.GetAll().AsQueryable();
            applicationsQuery = _tenantService.ApplyTenantFilter(applicationsQuery);
            var applications = applicationsQuery.ToList();
            
            var viewModel = new ApplicationScoreDetailsViewModel
            {
                Application = application,
                ScoreBreakdown = breakdown,
                TotalScore = breakdown.Sum(b => b.Score),
                MaxScore = maxScore,
                Percentage = maxScore > 0 ? (breakdown.Sum(b => b.Score) / maxScore) * 100 : 0,
                Rank = applications.OrderByDescending(a => a.Score).ToList().IndexOf(application) + 1
            };

            return View(viewModel);
        }

        /// <summary>
        /// Initiate interview scheduling workflow for a candidate
        /// </summary>
        public ActionResult ScheduleInterview(int applicationId)
        {
            // Interview scheduling to be implemented
            return HttpNotFound();
        }

        // Questions management (CRUD)
        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult Questions()
        {
            // Use eager loading to get questions with their options in one query
            var questionsQuery = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
            
            // Apply tenant filter
            questionsQuery = _tenantService.ApplyTenantFilter(questionsQuery);
            
            var questions = questionsQuery
                .OrderByDescending(q => q.Id) // Order by descending ID (newest first)
                .ToList();
            var list = questions
                .Select(q => new QuestionAdminViewModel
                {
                    Id = q.Id,
                    Text = q.Text,
                    Type = q.Type,
                    IsActive = q.IsActive,
                    Options = q.QuestionOptions.Select(o => new QuestionOptionVM
                    {
                        Id = o.Id,
                        Text = o.Text,
                        Points = o.Points
                    }).ToList()
                }).ToList();
            // Ensure positions are available for consolidated AI generation modal
            ViewBag.Positions = _uow.Positions.GetAll().ToList();
            // Use the combined AI-enhanced questions view
            return View("QuestionsWithMCP", list);
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult EditQuestion(int? id)
        {
            if (id == null)
            {
                return View(new QuestionAdminViewModel { IsActive = true });
            }
            var question = _uow.Questions.GetAll(q => q.QuestionOptions).FirstOrDefault(x => x.Id == id.Value);
            if (question == null)
                return HttpNotFound();
            var vm = new QuestionAdminViewModel
            {
                Id = question.Id,
                Text = question.Text,
                Type = question.Type,
                IsActive = question.IsActive,
                Options = question.QuestionOptions.Select(o => new QuestionOptionVM
                {
                    Id = o.Id,
                    Text = o.Text,
                    Points = o.Points
                }).ToList()
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult EditQuestion(QuestionAdminViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            Question q;
            var isUpdate = model.Id.HasValue;
            var oldValues = new object();
            
            try
            {
                if (isUpdate)
                {
                    q = _uow.Questions.Get(model.Id.Value);
                    if (q == null) return HttpNotFound();
                    
                    // Store old values for audit
                    oldValues = new { Text = q.Text, Type = q.Type, IsActive = q.IsActive };
                    
                    q.Text = model.Text;
                    q.Type = model.Type;
                    q.IsActive = model.IsActive;
                    _uow.Questions.Update(q);
                    var oldOptions = _uow.Context.Set<QuestionOption>().Where(o => o.QuestionId == q.Id);
                    _uow.Context.Set<QuestionOption>().RemoveRange(oldOptions);
                }

                else
                {
                    // create new
                    q = new Question
                    {
                        Text = model.Text,
                        Type = model.Type,
                        IsActive = model.IsActive
                    };
                    
                    // Assign company
                    var companyId = _tenantService.GetCurrentUserCompanyId();
                    if (companyId.HasValue)
                    {
                        q.CompanyId = companyId.Value;
                    }
                    
                    _uow.Questions.Add(q);
                    _uow.Complete();
                }
                _uow.Complete(); // Save question so it exists for option linking

                // Add options (allowed for any question type)
                var options = new List<object>();
                if (model.Options != null)
                {
                    foreach (var opt in model.Options)
                    {
                        if (!string.IsNullOrWhiteSpace(opt.Text))
                        {
                            var newOpt = new QuestionOption
                            {
                                QuestionId = q.Id,
                                Text = opt.Text,
                                Points = opt.Points
                            };
                            _uow.Context.Set<QuestionOption>().Add(newOpt);
                            options.Add(new { Text = opt.Text, Points = opt.Points });
                        }
                    }
                }
                _uow.Complete();
                
                // Log the action
                var newValues = new { 
                    Text = q.Text, 
                    Type = q.Type, 
                    IsActive = q.IsActive,
                    Options = options 
                };
                
                if (isUpdate)
                {
                    _auditService.LogUpdate(User.Identity.Name, "Admin", q.Id.ToString(), oldValues, newValues);
                }
                else
                {
                    _auditService.LogCreate(User.Identity.Name, "Admin", q.Id.ToString(), newValues);
                }
                
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

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteQuestion(int id)
        {
            var q = _uow.Questions.Get(id);
            if (q == null) return HttpNotFound();

            // Check if question belongs to current tenant
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && q.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }
            
            try
            {
                // Store question details for audit log before deletion
                var questionText = q.Text;
                
                // Delete related records in proper order due to foreign key constraints
                
                // 1. Delete ApplicationAnswer records that reference this question
                var applicationAnswers = _uow.Context.Set<ApplicationAnswer>().Where(aa => aa.QuestionId == id);
                _uow.Context.Set<ApplicationAnswer>().RemoveRange(applicationAnswers);
                
                // 2. Delete PositionQuestionOption records (through QuestionOptions)
                var questionOptions = _uow.Context.Set<QuestionOption>().Where(qo => qo.QuestionId == id).ToList();
                foreach (var option in questionOptions)
                {
                    // Delete PositionQuestionOption records that reference this QuestionOption
                    var positionQuestionOptions = _uow.Context.Set<PositionQuestionOption>().Where(pqo => pqo.QuestionOptionId == option.Id);
                    _uow.Context.Set<PositionQuestionOption>().RemoveRange(positionQuestionOptions);
                }
                
                // 3. Delete QuestionOption records
                _uow.Context.Set<QuestionOption>().RemoveRange(questionOptions);
                
                // 4. Delete PositionQuestion records that reference this question
                var positionQuestions = _uow.Context.Set<PositionQuestion>().Where(pq => pq.QuestionId == id);
                _uow.Context.Set<PositionQuestion>().RemoveRange(positionQuestions);
                
                // 5. Finally delete the question itself
                _uow.Questions.Remove(q);
                
                _uow.Complete();
                
                // Create audit log entry using AuditService
                _auditService.LogDelete(User.Identity.Name, "Admin", id.ToString(), new { QuestionText = questionText });
                
                TempData["Message"] = "Question deleted successfully.";
            }
            catch (Exception ex)
            {
                // Create audit log entry for failed deletion using AuditService
                _auditService.LogAction(User.Identity.Name, "DELETE", "Admin", id.ToString(), 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                TempData["Error"] = "Error deleting question: " + ex.Message;
                // Log the full exception for debugging
                System.Diagnostics.Debug.WriteLine("DeleteQuestion Error: " + ex.ToString());
            }
            
            return RedirectToAction("Questions");
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult BatchDeleteQuestions(int[] questionIds)
        {
            if (questionIds == null || questionIds.Length == 0)
            {
                return Json(new { success = false, message = "No questions selected for deletion." });
            }

            try
            {
                int deletedCount = 0;
                var deletedQuestions = new List<string>();
                
                foreach (var id in questionIds)
                {
                    var q = _uow.Questions.Get(id);
                    if (q == null) continue;

                    // Check if question belongs to current tenant
                    var companyId = _tenantService.GetCurrentUserCompanyId();
                    if (companyId.HasValue && q.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
                    {
                        continue; // Skip questions not belonging to tenant
                    }
                    
                    // Store question details for audit log before deletion
                    deletedQuestions.Add("ID: " + id + ", Text: " + q.Text);
                    
                    // Delete related records in proper order due to foreign key constraints
                    
                    // 1. Delete ApplicationAnswer records that reference this question
                    var applicationAnswers = _uow.Context.Set<ApplicationAnswer>().Where(aa => aa.QuestionId == id);
                    _uow.Context.Set<ApplicationAnswer>().RemoveRange(applicationAnswers);
                    
                    // 2. Delete PositionQuestionOption records (through QuestionOptions)
                    var questionOptions = _uow.Context.Set<QuestionOption>().Where(qo => qo.QuestionId == id).ToList();
                    foreach (var option in questionOptions)
                    {
                        // Delete PositionQuestionOption records that reference this QuestionOption
                        var positionQuestionOptions = _uow.Context.Set<PositionQuestionOption>().Where(pqo => pqo.QuestionOptionId == option.Id);
                        _uow.Context.Set<PositionQuestionOption>().RemoveRange(positionQuestionOptions);
                    }
                    
                    // 3. Delete QuestionOption records
                    _uow.Context.Set<QuestionOption>().RemoveRange(questionOptions);
                    
                    // 4. Delete PositionQuestion records that reference this question
                    var positionQuestions = _uow.Context.Set<PositionQuestion>().Where(pq => pq.QuestionId == id);
                    _uow.Context.Set<PositionQuestion>().RemoveRange(positionQuestions);
                    
                    // 5. Finally delete the question itself
                    _uow.Questions.Remove(q);
                    
                    deletedCount++;
                }
                
                _uow.Complete();
                
                // Create audit log entry for batch deletion using AuditService
                _auditService.LogAction(User.Identity.Name, "BATCH_DELETE", "Admin", 
                    string.Join(",", questionIds), 
                    new { DeletedCount = deletedCount, Questions = deletedQuestions });
                
                TempData["Message"] = string.Format("Successfully deleted {0} question(s).", deletedCount);
                return Json(new { success = true, message = string.Format("Successfully deleted {0} question(s).", deletedCount) });
            }
            catch (Exception ex)
            {
                // Create audit log entry for failed batch deletion using AuditService
                _auditService.LogAction(User.Identity.Name, "BATCH_DELETE", "Admin", 
                    string.Join(",", questionIds), 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                return Json(new { success = false, message = "Error deleting questions: " + ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult ExportQuestionBank()
        {
            try
            {
                var questionsQuery = _uow.Questions.GetAll().AsQueryable();
                // Apply tenant filter to export as well
                questionsQuery = _tenantService.ApplyTenantFilter(questionsQuery);
                var questions = questionsQuery.ToList();

                var csv = new System.Text.StringBuilder();
                
                // CSV Header
                csv.AppendLine("ID,Question Text,Question Type,Status,Created Date");
                
                // CSV Data
                foreach (var question in questions)
                {
                    var status = question.IsActive != false ? "Active" : "Inactive";
                    var createdDate = ""; // Question model doesn't have CreatedDate property
                    
                    // Escape commas and quotes in question text
                    var questionText = (question.Text != null ? question.Text.Replace("\"", "\"\"") : null) ?? "";
                    if (questionText.Contains(","))
                    {
                        questionText = "\"" + questionText + "\"";
                    }
                    
                    csv.AppendLine(string.Format("{0},{1},{2},{3},{4}", question.Id, questionText, question.Type, status, createdDate));
                }
                
                // Create audit log entry for export
                _auditService.LogAction(User.Identity.Name, "EXPORT_QUESTION_BANK", "Admin", 
                    questions.Count.ToString(), 
                    new { ExportedCount = questions.Count, Format = "CSV" });
                
                return Json(new { 
                    success = true, 
                    data = csv.ToString(), 
                    message = string.Format("Successfully exported {0} questions.", questions.Count) 
                });
            }
            catch (Exception ex)
            {
                // Create audit log entry for failed export
                _auditService.LogAction(User.Identity.Name, "EXPORT_QUESTION_BANK", "Admin", 
                    "0", 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                return Json(new { success = false, message = "Error exporting question bank: " + ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult AddToSampleQuestions(string questionsJson)
        {
            try
            {
                // This method is similar to AddGeneratedQuestionsToSample but works with existing questions
                var questions = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(questionsJson);
                
                // Check for duplicates in the existing sample questions
                var existingQuestionsQuery = _uow.Questions.GetAll().AsQueryable();
                existingQuestionsQuery = _tenantService.ApplyTenantFilter(existingQuestionsQuery);
                var existingQuestions = existingQuestionsQuery.ToList();
                var duplicates = new List<object>();
                var newQuestions = new List<object>();

                foreach (var question in questions)
                {
                    var questionText = question["text"].ToString();
                    var questionType = question["type"].ToString();

                    // Check for similar questions
                    var similarQuestion = existingQuestions.FirstOrDefault(eq => 
                        eq.Text.ToLower().Contains(questionText.ToLower().Substring(0, Math.Min(50, questionText.Length))) ||
                        questionText.ToLower().Contains(eq.Text.ToLower().Substring(0, Math.Min(50, eq.Text.Length))));

                    if (similarQuestion != null)
                    {
                        duplicates.Add(new
                        {
                            id = question["id"],
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
                            questionData = question
                        });
                    }
                }

                if (duplicates.Any())
                {
                    return Json(new { 
                        success = true, 
                        requiresDecision = true,
                        duplicates = duplicates, 
                        newQuestions = newQuestions,
                        message = string.Format("Found {0} potential duplicates. Please review before adding.", duplicates.Count)
                    });
                }
                else
                {
                    // No duplicates, just add them to sample (they're already in the main question bank)
                    return Json(new { 
                        success = true, 
                        requiresDecision = false,
                        message = string.Format("All {0} questions are already in the question bank.", questions.Count)
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding questions to sample: " + ex.Message });
            }
        }


        #region User Management

        /// <summary>
        /// Display all registered users with their account status and role management options
        /// Only Admin role can access this
        /// </summary>
        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult UserManagement()
        {
            var usersQuery = _uow.Users.GetAll().AsQueryable();
            usersQuery = _tenantService.ApplyTenantFilter(usersQuery);
            var users = usersQuery.ToList();
            var userViewModels = new List<UserManagementViewModel>();

            foreach (var user in users)
            {
                var isLocked = _securityService.IsAccountLocked(user.UserName);
                var lockoutEndTime = _securityService.GetLockoutEndTime(user.UserName);
                var failedAttempts = _securityService.GetRemainingAttempts(user.UserName);
                var actualFailedAttempts = 5 - failedAttempts; // Calculate actual failed attempts

                // Get last login info from audit logs
                var lastLogin = _uow.AuditLogs.GetAll()
                    .Where(a => a.Username == user.UserName && a.Action == "LOGIN_SUCCESS")
                    .OrderByDescending(a => a.Timestamp)
                    .FirstOrDefault();

                userViewModels.Add(new UserManagementViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    UserName = user.UserName,
                    Email = user.Email,
                    Role = user.Role,

                    Phone = _uow.Applicants.GetAll() // Applicants are already tenant filtered if TenantService is applied, but here we query by email
                        .Where(a => a.Email == user.Email)
                        .Select(a => a.Phone)
                        .FirstOrDefault(),
                    LastLoginDate = lastLogin != null ? (DateTime?)lastLogin.Timestamp : null,
                    LastLoginIP = lastLogin != null ? lastLogin.IPAddress : null,
                    IsLocked = isLocked,
                    LockoutEndTime = lockoutEndTime,
                    FailedLoginAttempts = actualFailedAttempts,
                    CreatedDate = user.Id > 0 ? DateTime.Now.AddDays(-30) : DateTime.Now // Placeholder since we don't have created date
                });
            }

            return View(userViewModels);
        }

        /// <summary>
        /// Display form to create a new user
        /// Only SuperAdmin role can access this
        /// </summary>
        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult CreateUser()
        {
            if (!_tenantService.IsActualSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied: Only SuperAdmins can manually create users.");
            }

            var viewModel = new CreateUserViewModel
            {
                Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList(),
                RequirePasswordChange = true
            };

            return View(viewModel);
        }

        /// <summary>
        /// Handle the creation of a new user
        /// Only SuperAdmin role can access this
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult CreateUser(CreateUserViewModel model)
        {
            if (!_tenantService.IsActualSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            if (!ModelState.IsValid)
            {
                model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
                return View(model);
            }

            // Check if username already exists within the same company
            var existingUserInCompany = _uow.Users.GetAll().FirstOrDefault(u => 
                u.UserName.Equals(model.UserName, StringComparison.OrdinalIgnoreCase) && 
                u.CompanyId == model.CompanyId);

            if (existingUserInCompany != null)
            {
                ModelState.AddModelError("UserName", "Username already exists within this company.");
                model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
                return View(model);
            }

            // Check if email already exists
            var existingEmail = _uow.Users.GetAll().FirstOrDefault(u => u.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));
            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "Email already exists.");
                model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
                return View(model);
            }

            try
            {
                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    UserName = model.UserName,
                    Email = model.Email,
                    Role = model.Role,
                    PasswordHash = PasswordHelper.HashPassword(model.Password),
                    CompanyId = model.CompanyId,
                    RequirePasswordChange = model.RequirePasswordChange
                };

                _uow.Users.Add(user);
                _uow.Complete();

                if (model.Role == "Client")
                {
                    var applicant = new Applicant
                    {
                        FullName = string.Format("{0} {1}", model.FirstName, model.LastName),
                        Email = model.Email,
                        Phone = model.Phone,
                        CompanyId = model.CompanyId
                    };
                    _uow.Applicants.Add(applicant);
                    _uow.Complete();
                }

                _auditService.LogAction(
                    User.Identity.Name,
                    "USER_CREATED",
                    "UserManagement",
                    user.Id.ToString(),
                    true,
                    string.Format("Created user {0} ({1}) with role {2}", user.UserName, user.Email, user.Role)
                );

                TempData["SuccessMessage"] = string.Format("User {0} has been created successfully.", user.UserName);
                return RedirectToAction("GlobalUserManagement");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
                return View(model);
            }
        }

        /// <summary>
        /// Display form to update user role
        /// Only Admin role can access this
        /// </summary>
        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult UpdateUserRole(int id)
        {
            var user = _uow.Users.Get(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            // Check if user belongs to current tenant
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && user.CompanyId != companyId.Value && !_tenantService.IsActualSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied: User belongs to another company.");
            }

            var viewModel = new UserRoleUpdateViewModel
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName,
                Email = user.Email,
                Phone = _uow.Applicants.GetAll()
                    .Where(a => a.Email == user.Email)
                    .Select(a => a.Phone)
                    .FirstOrDefault(),
                CurrentRole = user.Role,
                NewRole = user.Role,
                CompanyId = user.CompanyId,
                CurrentCompanyId = user.CompanyId,
                RequirePasswordChange = user.RequirePasswordChange,
                CurrentRequirePasswordChange = user.RequirePasswordChange,
                Companies = _tenantService.IsActualSuperAdmin() ? _uow.Companies.GetAll().OrderBy(c => c.Name).ToList() : null
            };

            return View(viewModel);
        }

        /// <summary>
        /// Handle user role update
        /// Only Admin role can access this
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateUserRole(UserRoleUpdateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                if (_tenantService.IsActualSuperAdmin())
                {
                    model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
                }
                return View(model);
            }

            var user = _uow.Users.Get(model.UserId);
            if (user == null)
            {
                return HttpNotFound();
            }

            // Check for duplicate Username
            if (user.UserName != model.UserName)
            {
                var existingUser = _uow.Users.GetAll().FirstOrDefault(u => u.UserName == model.UserName && u.Id != user.Id);
                if (existingUser != null)
                {
                    ModelState.AddModelError("UserName", "This username is already taken.");
                }
            }

            // Check for duplicate Email
            if (user.Email != model.Email)
            {
                var existingUser = _uow.Users.GetAll().FirstOrDefault(u => u.Email == model.Email && u.Id != user.Id);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "This email address is already in use.");
                }
            }

            if (!ModelState.IsValid)
            {
                if (_tenantService.IsActualSuperAdmin())
                {
                    model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
                }
                return View(model);
            }

            // Check if user belongs to current tenant
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && user.CompanyId != companyId.Value && !_tenantService.IsActualSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            // Role change restrictions for Admin users
            if (!_tenantService.IsActualSuperAdmin())
            {
                // Admin users can only change Client users to Admin role
                if (user.Role != "Client" && model.NewRole != "Client")
                {
                    TempData["ErrorMessage"] = "As an Admin, you can only change Client users to Admin role. You cannot modify roles of other Admin users or change roles to SuperAdmin.";
                    return RedirectToAction("UserManagement");
                }
                
                // Admin users cannot assign SuperAdmin role
                if (model.NewRole == "SuperAdmin")
                {
                    TempData["ErrorMessage"] = "Admin users cannot assign SuperAdmin role to any user.";
                    return RedirectToAction("UserManagement");
                }
                
                // Admin users can only change Client to Admin (not Admin to Client)
                if (user.Role == "Admin" && model.NewRole == "Client")
                {
                    TempData["ErrorMessage"] = "As an Admin, you cannot downgrade other Admin users to Client role.";
                    return RedirectToAction("UserManagement");
                }
            }

            var oldRole = user.Role;
            var oldCompanyId = user.CompanyId;
            var oldRequirePasswordChange = user.RequirePasswordChange;
            var oldFirstName = user.FirstName;
            var oldLastName = user.LastName;
            var oldUserName = user.UserName;
            var oldEmail = user.Email;
            var oldPhone = user.Phone;

            // Check if email changed and is unique
            if (user.Email != model.Email)
            {
                var existingUser = _uow.Users.GetAll().FirstOrDefault(u => u.Email == model.Email && u.Id != user.Id);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "This email address is already in use.");
                    return View(model);
                }
                
                // Regenerate token to force relogin on identity change
                user.AccessToken = _securityService.GenerateSecureToken();
            }

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.UserName = model.UserName;
            user.Email = model.Email;
            user.Phone = model.Phone;
            user.Role = model.NewRole;
            
            // If identity or role changed, invalidate existing sessions by regenerating the token
            if (oldUserName != user.UserName || oldRole != user.Role)
            {
                user.AccessToken = _securityService.GenerateSecureToken();
            }

            if (_tenantService.IsActualSuperAdmin())
            {
                user.CompanyId = model.CompanyId;
                user.RequirePasswordChange = model.RequirePasswordChange;
            }

            try
            {
                // Sync with Applicant record if it exists (lookup by old email in case it changed)
                var applicant = _uow.Context.Set<Applicant>().FirstOrDefault(a => a.Email == oldEmail);
                if (applicant != null)
                {
                    applicant.FullName = string.Format("{0} {1}", model.FirstName, model.LastName);
                    applicant.Email = model.Email; // Sync email change
                    applicant.Phone = model.Phone;
                    applicant.CompanyId = user.CompanyId; // Ensure company sync
                }
                else if (model.NewRole == "Client")
                {
                    // Create applicant record if it's a client but no record exists
                    var newApplicant = new Applicant
                    {
                        FullName = string.Format("{0} {1}", model.FirstName, model.LastName),
                        Email = user.Email,
                        Phone = model.Phone,
                        CompanyId = user.CompanyId
                    };
                    _uow.Applicants.Add(newApplicant);
                }

                // Single save for all changes (User update, Applicant update/create)
                _uow.Complete();

                // Log the update
                _auditService.LogUpdate(
                    User.Identity.Name,
                    "Account",
                    user.Id.ToString(),
                    new { FirstName = oldFirstName, LastName = oldLastName, UserName = oldUserName, Email = oldEmail, Role = oldRole, CompanyId = oldCompanyId },
                    new { FirstName = user.FirstName, LastName = user.LastName, UserName = user.UserName, Email = user.Email, Role = model.NewRole, CompanyId = user.CompanyId }
                );
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errorMessages = new List<string>();
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        errorMessages.Add(string.Format("Property: {0} Error: {1}", validationError.PropertyName, validationError.ErrorMessage));
                        System.Diagnostics.Debug.WriteLine(string.Format("Property: {0} Error: {1}", validationError.PropertyName, validationError.ErrorMessage));
                    }
                }
                TempData["ErrorMessage"] = "Data Validation Error: " + string.Join("; ", errorMessages);
                
                if (_tenantService.IsActualSuperAdmin())
                {
                    model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
                }
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating user: " + ex.Message;
                if (_tenantService.IsActualSuperAdmin())
                {
                    model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
                }
                return View(model);
            }

            string successMsg = string.Format("User {0} updated successfully.", user.UserName);
            if (oldRole != user.Role)
            {
                successMsg += string.Format(" Role changed from {0} to {1}.", oldRole, user.Role);
            }

            TempData["SuccessMessage"] = successMsg;
            
            // Sync session if updating current user
            if (user.UserName == User.Identity.Name)
            {
                Session["IsActualSuperAdmin"] = _tenantService.IsActualSuperAdmin();
            }

            // Redirect based on authority level
            if (_tenantService.IsActualSuperAdmin() || User.IsInRole("SuperAdmin"))
            {
                return RedirectToAction("GlobalUserManagement");
            }
            
            return RedirectToAction("UserManagement");
        }

        /// <summary>
        /// Unlock a locked user account
        /// Only Admin role can access this
        /// </summary>
        [Authorize(Roles = "Admin, SuperAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UnlockUserAccount(int id)
        {
            var user = _uow.Users.Get(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            // Check if user belongs to current tenant
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && user.CompanyId != companyId.Value && !_tenantService.IsActualSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            // Clear failed login attempts
            _securityService.ClearFailedAttempts(user.UserName);

            // Log the account unlock
            _auditService.LogAction(
                User.Identity.Name,
                "ACCOUNT_UNLOCKED",
                "Account",
                user.Id.ToString(),
                null,
                new { UnlockedBy = User.Identity.Name, UnlockedAt = DateTime.Now }
            );

            TempData["SuccessMessage"] = string.Format("User {0} account has been unlocked", user.UserName);
            
            if (_tenantService.IsActualSuperAdmin() || User.IsInRole("SuperAdmin"))
            {
                return RedirectToAction("GlobalUserManagement");
            }

            return RedirectToAction("UserManagement");
        }

        #endregion

        #region Security Logs

        /// <summary>
        /// Display security logs (login attempts and audit logs)
        /// Only Admin role can access this
        /// </summary>
        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult SecurityLogs(LogFilter filter)
        {
            // Detect if this is a fresh page load (no parameters) vs. a form execution.
            // MVC Model Binder auto-populates 'Action' and 'Controller' from route data IF they exist in the model.
            // We must clear them so we don't accidentally filter to "Admin" controller and "SecurityLogs" action.
            bool isFreshLoad = Request.QueryString.Count == 0;
            
            if (filter == null)
            {
                filter = new LogFilter();
            }
            
            if (isFreshLoad)
            {
                filter.Username = null;
                filter.Action = null;
                filter.Controller = null;
            }
            else
            {
                // Even on POST/Search, if they match the route exactly, they might be collisions
                if (string.Equals(filter.Action, "SecurityLogs", StringComparison.OrdinalIgnoreCase)) filter.Action = null;
                if (string.Equals(filter.Controller, "Admin", StringComparison.OrdinalIgnoreCase)) filter.Controller = null;
            }

            var viewModel = new SecurityLogsViewModel
            {
                Filter = filter
            };

            // Get login attempts
            var loginAttemptsQuery = _uow.LoginAttempts.GetAll().AsQueryable();
            loginAttemptsQuery = _tenantService.ApplyTenantFilter(loginAttemptsQuery);
            
            if (!string.IsNullOrEmpty(viewModel.Filter.Username))
                loginAttemptsQuery = loginAttemptsQuery.Where(l => l.Username.Contains(viewModel.Filter.Username));
            
            if (!string.IsNullOrEmpty(viewModel.Filter.IPAddress))
                loginAttemptsQuery = loginAttemptsQuery.Where(l => l.IPAddress.Contains(viewModel.Filter.IPAddress));
            
            if (viewModel.Filter.WasSuccessful.HasValue)
                loginAttemptsQuery = loginAttemptsQuery.Where(l => l.WasSuccessful == viewModel.Filter.WasSuccessful.Value);
            
            if (viewModel.Filter.StartDate.HasValue)
                loginAttemptsQuery = loginAttemptsQuery.Where(l => l.AttemptTime >= viewModel.Filter.StartDate.Value);
            
            if (viewModel.Filter.EndDate.HasValue)
            {
                var nextDay = viewModel.Filter.EndDate.Value.Date.AddDays(1);
                loginAttemptsQuery = loginAttemptsQuery.Where(l => l.AttemptTime < nextDay);
            }

            var loginAttempts = loginAttemptsQuery
                .OrderByDescending(l => l.AttemptTime)
                .Take(1000) // Limit to prevent performance issues
                .ToList();

            viewModel.LoginAttempts = loginAttempts.Select(l => new LoginAttemptLog
            {
                Id = l.Id,
                Username = l.Username,
                IPAddress = l.IPAddress,
                AttemptTime = l.AttemptTime,
                WasSuccessful = l.WasSuccessful,
                FailureReason = l.FailureReason
            }).ToList();

            // Get audit logs
            var auditLogsQuery = _uow.AuditLogs.GetAll().AsQueryable();
            auditLogsQuery = _tenantService.ApplyTenantFilter(auditLogsQuery);
            
            if (!string.IsNullOrEmpty(viewModel.Filter.Username))
                auditLogsQuery = auditLogsQuery.Where(a => a.Username.Contains(viewModel.Filter.Username));
            
            // Filter by Action (ignore if it's the current action name)
            if (!string.IsNullOrEmpty(viewModel.Filter.Action))
            {
                var actionMatch = viewModel.Filter.Action.ToLower();
                // Ignore Action filter if it's "SecurityLogs" as this is not a valid audit log action
                if (!string.Equals(actionMatch, "securitylogs", StringComparison.OrdinalIgnoreCase))
                {
                    auditLogsQuery = auditLogsQuery.Where(a => a.Action.ToLower().Contains(actionMatch));
                }
            }
            
            if (!string.IsNullOrEmpty(viewModel.Filter.Controller))
                auditLogsQuery = auditLogsQuery.Where(a => a.Controller.Contains(viewModel.Filter.Controller));
            
            if (!string.IsNullOrEmpty(viewModel.Filter.IPAddress))
                auditLogsQuery = auditLogsQuery.Where(a => a.IPAddress.Contains(viewModel.Filter.IPAddress));
            
            if (viewModel.Filter.WasSuccessful.HasValue)
                auditLogsQuery = auditLogsQuery.Where(a => a.WasSuccessful == viewModel.Filter.WasSuccessful.Value);
            
            if (viewModel.Filter.StartDate.HasValue)
                auditLogsQuery = auditLogsQuery.Where(a => a.Timestamp >= viewModel.Filter.StartDate.Value);
            
            if (viewModel.Filter.EndDate.HasValue)
            {
                var nextDay = viewModel.Filter.EndDate.Value.Date.AddDays(1);
                auditLogsQuery = auditLogsQuery.Where(a => a.Timestamp < nextDay);
            }

            var auditLogs = auditLogsQuery
                .OrderByDescending(a => a.Timestamp)
                .Take(1000) // Limit to prevent performance issues
                .ToList();

            viewModel.AuditLogs = auditLogs.Select(a => new AuditLogEntry
            {
                Id = a.Id,
                Username = a.Username,
                Action = a.Action,
                Controller = a.Controller,
                EntityId = a.EntityId,
                IPAddress = a.IPAddress,
                Timestamp = a.Timestamp,
                UserAgent = a.UserAgent,
                WasSuccessful = a.WasSuccessful,
                ErrorMessage = a.ErrorMessage
            }).ToList();

            // Calculate statistics
            // Calculate statistics with tenant filtering
            viewModel.TotalLoginAttempts = loginAttemptsQuery.Count();
            viewModel.TotalAuditLogs = auditLogsQuery.Count();
            viewModel.FailedLoginAttempts = loginAttemptsQuery.Count(l => !l.WasSuccessful);
            viewModel.SuccessfulLogins = loginAttemptsQuery.Count(l => l.WasSuccessful);

            return View(viewModel);
        }

        #endregion
    public class ApplicationScoreDetailsViewModel
    {
        public Application Application { get; set; }
        public List<QuestionScoreBreakdown> ScoreBreakdown { get; set; }
        public decimal TotalScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
        public int Rank { get; set; }
    }
}
}
