using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;
using HR.Web.Helpers;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    [ModuleAccess(RoleModuleCatalog.Positions)]
    public partial class PositionsController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly AuditService _auditService = new AuditService();
        private readonly TenantService _tenantService = new TenantService();

        private string GetCurrentActorName()
        {
            return User?.Identity?.Name ?? "System";
        }

        [AllowAnonymous]
        public ActionResult Index(int? companyId = null)
        {
            CloseExpiredPositionsForCurrentScope(companyId);
            bool isSuperAdmin = _tenantService.IsSuperAdmin();
            var rolePermissionService = new RolePermissionService();
            ViewBag.IsSuperAdmin = isSuperAdmin;
            
            if (isSuperAdmin)
            {
                System.Collections.Generic.IEnumerable<Position> positionsList;
                if (!companyId.HasValue)
                {
                    // Show ALL positions from ALL companies
                    ViewBag.SelectedCompanyName = "All Companies";
                    ViewBag.SelectedCompanyId = null;
                    positionsList = _uow.Positions.GetAll(p => p.Department, p => p.Company);
                }
                else
                {
                    // SuperAdmin with company selected -> show ONLY that company's positions
                    var company = _uow.Companies.Get(companyId.Value);
                    ViewBag.SelectedCompanyName = company != null ? company.Name : "Unknown Company";
                    ViewBag.SelectedCompanyId = companyId.Value;
                    
                    positionsList = _uow.Positions.GetAll(p => p.Department, p => p.Company)
                        .Where(p => p.CompanyId == companyId.Value);
                }

                // SuperAdmins view positions as read-only
                ViewBag.IsAdmin = false;
                ViewBag.IsReadOnly = true;
                ViewBag.IsSuperAdmin = true;
                
                // Fetch all companies for the visibility toggle UI
                ViewBag.AllCompanies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();

                return View(positionsList.OrderByDescending(p => p.PostedOn).ToList());
            }

            // Public/Standard logic
            var query = _uow.Positions.GetAll(p => p.Department, p => p.Company).AsQueryable();
            
            // Apply public-aware tenant filtering
            query = _tenantService.ApplyPublicTenantFilter(query);
            
            bool isAuthenticated = User?.Identity != null && User.Identity.IsAuthenticated;
            bool isAdminUser = isAuthenticated && User.IsInRole("Admin");
            bool canManagePositions = isAdminUser &&
                rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Positions, RoleAccessLevels.Manage);
            bool canViewPositions = isAdminUser &&
                rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Positions, RoleAccessLevels.View);
            bool isReadOnly = isAdminUser && canViewPositions && !canManagePositions;
            
            ViewBag.IsAdmin = canManagePositions;
            ViewBag.IsReadOnly = isReadOnly;
            
            // For non-admin users (clients/guests), only show open positions
            if (!canManagePositions && !isReadOnly)
            {
                query = query.Where(p => p.IsOpen);
            }
            
            var result = query.OrderByDescending(p => p.PostedOn).ToList();
            PopulateCandidatePositionActions(result, canManagePositions, isReadOnly);
            return View(result);
        }

        [AllowAnonymous]
        public ActionResult Details(int id)
        {
            var position = _uow.Positions.GetAll(p => p.Department)
                .FirstOrDefault(p => p.Id == id);
            if (position == null)
            {
                return HttpNotFound();
            }

            ClosePositionIfExpired(position);
            
            // Prevent non-admin users from accessing closed positions
            if (!position.IsOpen && (User == null || !User.IsInRole("Admin")))
            {
                return new HttpStatusCodeResult(403, "This position is not available for application.");
            }

            ViewBag.CanManagePositions = Request.IsAuthenticated &&
                new RolePermissionService().CanCurrentUserAccessModule(RoleModuleCatalog.Positions, RoleAccessLevels.Manage);
            ViewBag.IsReadOnly = Request.IsAuthenticated &&
                User != null &&
                User.IsInRole("Admin") &&
                new RolePermissionService().CanCurrentUserAccessModule(RoleModuleCatalog.Positions, RoleAccessLevels.View) &&
                !(bool)ViewBag.CanManagePositions;

            ViewBag.CandidateAction = ResolveCandidatePositionAction(position);
            
            return View(position);
        }

        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Create()
        {
            // SuperAdmin should not create positions
            if (_tenantService.IsSuperAdmin())
            {
                return RedirectToAction("Index");
            }
            
            var departmentList = _uow.Departments.GetAll().AsQueryable();
            departmentList = _tenantService.ApplyTenantFilter(departmentList);
            ViewBag.DepartmentId = new SelectList(departmentList.ToList(), "Id", "Name");
            
            // Load questions (filtered by tenant)
            var questionList = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
            questionList = _tenantService.ApplyTenantFilter(questionList);
            ViewBag.QuestionList = questionList.ToList();
            ViewBag.QuestionnaireTemplates = new QuestionnaireTemplateService().GetActiveTemplatesForCurrentTenant();
            
            return View(new Position
            {
                IsOpen = true,
                PostedOn = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.Date.AddMonths(3),
                PassMark = 50m
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Create(Position model, int[] selectedQuestions, string questionWeightValues, string questionStagesPayload)
        {
            var questionWeights = ParseQuestionWeights(questionWeightValues);
            return HandleCreatePosition(model, selectedQuestions, questionWeights, questionStagesPayload);
        }

        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Edit(int id)
        {
            var position = _uow.Positions.GetAll(p => p.PositionQuestions).FirstOrDefault(p => p.Id == id);
            if (position == null)
            {
                return HttpNotFound();
            }

            ClosePositionIfExpired(position);

            // Tenant check
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var departmentList = _uow.Departments.GetAll().AsQueryable();
            departmentList = _tenantService.ApplyTenantFilter(departmentList);
            ViewBag.DepartmentId = new SelectList(departmentList.ToList(), "Id", "Name", position.DepartmentId);
            
            // Load questions filtered by tenant
            var allQuestions = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
            allQuestions = _tenantService.ApplyTenantFilter(allQuestions);
            ViewBag.QuestionList = allQuestions.ToList();
            
            // Get currently selected question IDs for pre-checking
            var selectedQuestionIds = position.PositionQuestions != null ? position.PositionQuestions.Select(pq => pq.QuestionId).ToList() : new System.Collections.Generic.List<int>();
            ViewBag.SelectedQuestionIds = selectedQuestionIds;
            ViewBag.SelectedQuestionStages = position.PositionQuestions != null
                ? position.PositionQuestions
                    .Where(pq => pq != null)
                    .GroupBy(pq => pq.QuestionId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(pq => pq.StageNumber <= 0 ? 1 : pq.StageNumber).Distinct().OrderBy(s => s).ToList())
                : new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();

            ViewBag.QuestionnaireTemplates = new QuestionnaireTemplateService().GetActiveTemplatesForCurrentTenant();

            return View(position);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Edit(Position model, int[] selectedQuestions, string questionWeightValues, string questionStagesPayload)
        {
            var questionWeights = ParseQuestionWeights(questionWeightValues);
            return HandleEditPosition(model, selectedQuestions, questionWeights, questionStagesPayload);
        }

        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult DatabaseTest()
        {
            if (!AppConfig.IsRemoteDevelopment)
            {
                return HttpNotFound();
            }

            try
            {
                _uow.Context.Database.Connection.Open();
                _uow.Context.Database.Connection.Close();
                return Content("Database connection OK.", "text/plain");
            }
            catch (Exception ex)
            {
                return Content("Database connection failed: " + ex.Message, "text/plain");
            }
        }

        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult CreateTestData()
        {
            try
            {
                // Create a test choice question with options
                var testQuestion = new Question
                {
                    Text = "What is your preferred work environment?",
                    Type = "Choice",
                    IsActive = true
                };
                _uow.Questions.Add(testQuestion);
                _uow.Complete();
                
                // Add options for the question
                var options = new[]
                {
                    new QuestionOption { QuestionId = testQuestion.Id, Text = "Remote", Points = 5 },
                    new QuestionOption { QuestionId = testQuestion.Id, Text = "Office", Points = 3 },
                    new QuestionOption { QuestionId = testQuestion.Id, Text = "Hybrid", Points = 4 }
                };
                
                foreach (var option in options)
                {
                    _uow.Context.Set<QuestionOption>().Add(option);
                }
                _uow.Complete();
                
                return Json(new { success = true, message = "Test data created successfully", questionId = testQuestion.Id, optionsCount = options.Length });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult TestEagerLoading()
        {
            try
            {
                // Test eager loading - get questions with options (applying tenant filter)
                var query = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
                query = _tenantService.ApplyTenantFilter(query);
                var questionsWithOptions = query.ToList();
                
                var testResult = new
                {
                    Success = true,
                    TotalQuestions = questionsWithOptions.Count,
                    QuestionsWithOptions = questionsWithOptions.Count(q => q.QuestionOptions != null && q.QuestionOptions.Any()),
                    Questions = questionsWithOptions.Select(q => new
                    {
                        q.Id,
                        q.Text,
                        q.Type,
                        HasOptions = q.QuestionOptions != null,
                        OptionsCount = q.QuestionOptions != null ? q.QuestionOptions.Count() : 0,
                        Options = q.QuestionOptions != null ? q.QuestionOptions.Select(o => new
                        {
                            o.Id,
                            o.Text,
                            o.Points
                        }).ToList() : null
                    }).ToList()
                };
                
                return Json(testResult, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Success = false, Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult TestQuestionOptions()
        {
            // Apply tenant filtering to options and questions
            var questionsQuery = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
            questionsQuery = _tenantService.ApplyTenantFilter(questionsQuery);
            var allQuestions = questionsQuery.ToList();

            var allOptions = _uow.Context.Set<QuestionOption>().AsQueryable();
            // Since QuestionOption is an ITenantEntity (presumably, or we filter via Question)
            // Let's filter options based on the filtered questions to be safe
            var allowedQuestionIds = allQuestions.Select(q => q.Id).ToList();
            var filteredOptions = allOptions.Where(o => allowedQuestionIds.Contains(o.QuestionId)).ToList();
            
            var result = new
            {
                TotalQuestions = allQuestions.Count,
                TotalOptions = filteredOptions.Count,
                Questions = allQuestions.Select(q => new
                {
                    q.Id,
                    q.Text,
                    q.Type,
                    OptionsCount = q.QuestionOptions != null ? q.QuestionOptions.Count() : 0
                }).ToList(),
                Options = filteredOptions.Select(o => new
                {
                    o.Id,
                    o.Text,
                    o.Points,
                    o.QuestionId
                }).ToList()
            };
            
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private void CloseExpiredPositionsForCurrentScope(int? companyId)
        {
            var query = _uow.Positions.GetAll().AsQueryable();

            if (_tenantService.IsSuperAdmin())
            {
                if (companyId.HasValue)
                {
                    query = query.Where(p => p.CompanyId == companyId.Value);
                }
            }
            else
            {
                query = _tenantService.ApplyPublicTenantFilter(query);
            }

            var expiredOpenPositions = query
                .Where(p => p.IsOpen && p.ExpiryDate.HasValue)
                .ToList()
                .Where(p => HasReachedExpiry(p.ExpiryDate))
                .ToList();

            if (!expiredOpenPositions.Any())
            {
                return;
            }

            foreach (var expiredPosition in expiredOpenPositions)
            {
                if (expiredPosition == null)
                {
                    continue;
                }

                var scopedPosition = expiredPosition;
                scopedPosition.IsOpen = false;
                _uow.Positions.Update(scopedPosition);
            }

            _uow.Complete();
        }

        private void ClosePositionIfExpired(Position position)
        {
            if (position == null || !position.IsOpen || !HasReachedExpiry(position.ExpiryDate))
            {
                return;
            }

            position.IsOpen = false;
            _uow.Positions.Update(position);
            _uow.Complete();
        }

        private static bool HasReachedExpiry(DateTime? expiryDate)
        {
            return expiryDate.HasValue && expiryDate.Value.Date <= DateTime.UtcNow.Date;
        }

        private void PopulateCandidatePositionActions(IEnumerable<Position> positions, bool canManagePositions, bool isReadOnly)
        {
            if (canManagePositions || isReadOnly || positions == null)
            {
                ViewBag.CandidateActionsByPositionId = new Dictionary<int, PositionCandidateActionViewModel>();
                return;
            }

            var applicationsByPositionId = LoadCandidateApplicationsByPositionId(positions);
            var actions = positions.ToDictionary(
                p => p.Id,
                p => PositionCandidateActionHelper.Resolve(
                    Request.IsAuthenticated,
                    applicationsByPositionId.ContainsKey(p.Id) ? applicationsByPositionId[p.Id] : null,
                    p,
                    Url));

            ViewBag.CandidateActionsByPositionId = actions;
        }

        private PositionCandidateActionViewModel ResolveCandidatePositionAction(Position position)
        {
            if (position == null)
            {
                return null;
            }

            var canManagePositions = ViewBag.CanManagePositions != null && (bool)ViewBag.CanManagePositions;
            var isReadOnly = ViewBag.IsReadOnly != null && (bool)ViewBag.IsReadOnly;
            if (canManagePositions || isReadOnly)
            {
                return null;
            }

            Application existingApplication = null;
            if (Request.IsAuthenticated)
            {
                var applicationsByPositionId = LoadCandidateApplicationsByPositionId(new[] { position });
                applicationsByPositionId.TryGetValue(position.Id, out existingApplication);
            }

            return PositionCandidateActionHelper.Resolve(Request.IsAuthenticated, existingApplication, position, Url);
        }

        private Dictionary<int, Application> LoadCandidateApplicationsByPositionId(IEnumerable<Position> positions)
        {
            var result = new Dictionary<int, Application>();
            if (!Request.IsAuthenticated || positions == null || User?.Identity == null)
            {
                return result;
            }

            var email = User.Identity.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return result;
            }

            var positionList = positions.Where(p => p != null).ToList();
            if (!positionList.Any())
            {
                return result;
            }

            var positionIds = positionList.Select(p => p.Id).Distinct().ToList();
            var companyIds = positionList
                .Where(p => p.CompanyId.HasValue)
                .Select(p => p.CompanyId.Value)
                .Distinct()
                .ToList();
            if (!companyIds.Any())
            {
                return result;
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var applicantIds = _uow.Context.Set<Applicant>()
                .AsNoTracking()
                .Where(a => a.Email.ToLower() == normalizedEmail && companyIds.Contains(a.CompanyId.Value))
                .Select(a => a.Id)
                .ToList();
            if (!applicantIds.Any())
            {
                return result;
            }

            var applications = _uow.Context.Set<Application>()
                .AsNoTracking()
                .Where(a => applicantIds.Contains(a.ApplicantId) && positionIds.Contains(a.PositionId))
                .ToList();

            foreach (var application in applications)
            {
                result[application.PositionId] = application;
            }

            return result;
        }

        private static IDictionary<int, decimal> ParseQuestionWeights(string payload)
        {
            var weights = new Dictionary<int, decimal>();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return weights;
            }

            var entries = payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('=');
                if (parts.Length != 2)
                {
                    continue;
                }

                int questionId;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out questionId) || questionId <= 0)
                {
                    continue;
                }

                decimal weight;
                if (!decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out weight))
                {
                    continue;
                }

                weights[questionId] = Math.Max(0m, Math.Min(100m, weight));
            }

            return weights;
        }

        private static Dictionary<int, HashSet<int>> ParseQuestionStages(string payload, int[] selectedQuestions, int questionnaireStageCount)
        {
            return QuestionStagePayloadHelper.Parse(payload, selectedQuestions, questionnaireStageCount);
        }

        private static string ValidateQuestionnaireStageConfiguration(int questionnaireStageCount, int[] selectedQuestions, IDictionary<int, HashSet<int>> stages)
        {
            return QuestionStagePayloadHelper.ValidateAllStagesHaveQuestions(questionnaireStageCount, selectedQuestions, stages);
        }

        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Delete(int id)
        {
            var position = _uow.Positions.Get(id);
            if (position == null)
            {
                return HttpNotFound();
            }

            // Tenant check
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return View(position);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [TenantAuthorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult DeleteConfirmed(int id)
        {
            return HandleDeletePosition(id);
        }
        [HttpPost]
        [TenantAuthorize]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleCompanyVisibility(int companyId, bool isVisible)
        {
            if (!_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var company = _uow.Companies.Get(companyId);
            if (company == null)
            {
                return HttpNotFound();
            }

            // 'isVisible' maps to IsActive for public display purposes
            company.IsActive = isVisible;
            _uow.Companies.Update(company);
            _uow.Complete();

            return Json(new { success = true, newStatus = company.IsActive });
        }
    }
}

