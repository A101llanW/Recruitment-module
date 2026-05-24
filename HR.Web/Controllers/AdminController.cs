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
    [ModuleAccess]
    public partial class AdminController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly SecurityService _securityService = new SecurityService();
        private readonly AuditService _auditService = new AuditService();
        private readonly TenantService _tenantService = new TenantService();
        private readonly RolePermissionService _rolePermissionService = new RolePermissionService();

        // GET: Admin/Index - Default admin dashboard
        public ActionResult Index()
        {
            if (_tenantService.IsSuperAdmin())
            {
                return RedirectToAction("GlobalUserManagement");
            }

            if (_rolePermissionService.HasCurrentUserCustomAdminRole())
            {
                return RedirectToAction("Index", "Dashboard");
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
            return HandleCandidateRankings(positionId);
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
            var application = _uow.Applications.Get(
                applicationId,
                a => a.Applicant,
                a => a.Position);
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
                    AllowMultipleChoices = q.AllowMultipleChoices,
                    Options = q.QuestionOptions.Select(o => new QuestionOptionVM
                    {
                        Id = o.Id,
                        Text = o.Text,
                        Points = o.Points
                    }).ToList()
                }).ToList();
            // Ensure positions are available for consolidated AI generation modal
            ViewBag.Positions = _uow.Positions.GetAll(p => p.Department, p => p.Company).ToList();
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
                AllowMultipleChoices = question.AllowMultipleChoices,
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
            return HandleEditQuestion(model);
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
            return HandleAddToSampleQuestions(questionsJson);
        }


        #region User Management

        /// <summary>
        /// Display all registered users with their account status and role management options
        /// Only Admin role can access this
        /// </summary>
        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult UserManagement()
        {
            var usersQuery = _uow.Users.GetAll(u => u.RoleDefinition, u => u.Company).AsQueryable();
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
                    Role = _rolePermissionService.GetDisplayRole(user),
                    BaseRole = user.Role,

                    Phone = _uow.Applicants.GetAll() // Applicants are already tenant filtered if TenantService is applied, but here we query by email
                        .Where(a => a.Email == user.Email)
                        .Select(a => a.Phone)
                        .FirstOrDefault(),
                    CompanyName = user.Company != null ? user.Company.Name : "System",
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
                RequirePasswordChange = true,
                AvailableRoleOptions = BuildAvailableRoleOptions(true, null, null, false, null),
                SelectedRoleKey = "builtin:Client"
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
        public ActionResult CreateUser(CreateUserViewModel model)
        {
            if (model != null)
            {
                model.Password = Request.Unvalidated.Form["Password"];
                model.ConfirmPassword = Request.Unvalidated.Form["ConfirmPassword"];
            }

            return HandleCreateUser(model);
        }

        /// <summary>
        /// Display form to update user role
        /// Only Admin role can access this
        /// </summary>
        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult UpdateUserRole(int id)
        {
            var user = _uow.Users.GetAll(u => u.RoleDefinition).FirstOrDefault(u => u.Id == id);
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
                CurrentRoleDisplay = _rolePermissionService.GetDisplayRole(user),
                NewRole = user.Role,
                SelectedRoleKey = BuildRoleSelectionKey(user),
                CompanyId = user.CompanyId,
                CurrentCompanyId = user.CompanyId,
                CurrentRoleDefinitionId = user.RoleDefinitionId,
                IsCurrentFullAdmin = _rolePermissionService.IsFullCompanyAdmin(user),
                RequirePasswordChange = user.RequirePasswordChange,
                CurrentRequirePasswordChange = user.RequirePasswordChange,
                Companies = _tenantService.IsSuperAdmin() ? _uow.Companies.GetAll().OrderBy(c => c.Name).ToList() : null,
                AvailableRoleOptions = BuildAvailableRoleOptions(
                    _tenantService.IsSuperAdmin(),
                    _tenantService.GetCurrentUserCompanyId(),
                    user.CompanyId,
                    false,
                    BuildRoleSelectionKey(user))
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
            return HandleUserRoleUpdate(model);
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
            
            return RedirectToUserManagementHome();
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
            return HandleSecurityLogs(filter);
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
