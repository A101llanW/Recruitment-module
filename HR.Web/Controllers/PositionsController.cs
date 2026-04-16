using System;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;

namespace HR.Web.Controllers
{
    public class PositionsController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly AuditService _auditService = new AuditService();
        private readonly TenantService _tenantService = new TenantService();

        [AllowAnonymous]
        public ActionResult Index(int? companyId = null)
        {
            bool isSuperAdmin = _tenantService.IsSuperAdmin();
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
            
            bool isAuthenticated = User.Identity.IsAuthenticated;
            bool isAdmin = isAuthenticated && User.IsInRole("Admin");
            
            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsReadOnly = false;
            
            // For non-admin users (clients/guests), only show open positions
            if (!isAdmin)
            {
                query = query.Where(p => p.IsOpen);
            }
            
            var result = query.OrderByDescending(p => p.PostedOn).ToList();
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
            
            // Prevent non-admin users from accessing closed positions
            if (position != null && !position.IsOpen && (User == null || !User.IsInRole("Admin")))
            {
                return new HttpStatusCodeResult(403, "This position is not available for application.");
            }
            
            return View(position);
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
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
            
            return View(new Position
            {
                IsOpen = true,
                PostedOn = DateTime.UtcNow
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Create(Position model, int[] selectedQuestions)
        {
            // SuperAdmin should not create positions
            if (_tenantService.IsSuperAdmin())
            {
                return RedirectToAction("Index");
            }

            // Assign company before validation check since it's [Required] in the model
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue)
            {
                model.CompanyId = companyId.Value;
                // Clear the error if it was added due to missing form field
                if (ModelState.ContainsKey("CompanyId"))
                {
                    ModelState["CompanyId"].Errors.Clear();
                }
            }

            // ensure a department was selected (DropDownList optionLabel posts empty -> 0)
            if (model.DepartmentId <= 0)
            {
                ModelState.AddModelError("DepartmentId", "Please select a department.");
            }
            else
            {
                // Clear any default validation errors for DepartmentId if we have a valid value
                if (ModelState.ContainsKey("DepartmentId"))
                {
                    ModelState["DepartmentId"].Errors.Clear();
                }
            }

            Debug.WriteLine(string.Format("[PositionsController.Create][POST] Title='{0}', DeptId={1}, CompanyId={2}", 
                model != null ? model.Title : "", 
                model != null ? model.DepartmentId : 0,
                model != null ? model.CompanyId : 0));
            Debug.WriteLine("ModelState.IsValid = " + ModelState.IsValid);

            if (!ModelState.IsValid)
            {
                foreach (var kvp in ModelState)
                {
                    foreach (var err in kvp.Value.Errors)
                    {
                        Debug.WriteLine(string.Format("[PositionsController.Create][ModelError] Key='{0}', Error='{1}', Exception='{2}'", kvp.Key, err.ErrorMessage, err.Exception != null ? err.Exception.Message : ""));
                    }
                }
                ViewBag.DepartmentId = new SelectList(_uow.Departments.GetAll(), "Id", "Name", model.DepartmentId);
                var allQuestions = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
                allQuestions = _tenantService.ApplyTenantFilter(allQuestions);
                ViewBag.QuestionList = allQuestions.ToList();
                
                Debug.WriteLine("[PositionsController.Create][POST] Returning view due to invalid ModelState.");
                return View(model);
            }

            model.PostedOn = DateTime.UtcNow;
            
            // Set default currency to KES if not provided
            if (string.IsNullOrEmpty(model.Currency))
            {
                model.Currency = "KES";
            }
            try
            {
                // Assign company
                Debug.WriteLine("[PositionsController.Create][POST] Adding position to UoW and saving...");
                _uow.Positions.Add(model);
                _uow.Complete();
                Debug.WriteLine("[PositionsController.Create][POST] Save succeeded. New Id=" + model.Id);
                
                // Log position creation
                var newValues = new { 
                    Title = model.Title, 
                    Description = model.Description, 
                    Responsibilities = model.Responsibilities,
                    Qualifications = model.Qualifications,
                    DepartmentId = model.DepartmentId,
                    Location = model.Location,
                    IsOpen = model.IsOpen,
                    PostedOn = model.PostedOn
                };
                _auditService.LogCreate(User.Identity.Name, "Positions", model.Id.ToString(), newValues);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PositionsController.Create][POST] Exception during save: " + ex);
                var msg = ex.GetBaseException() != null ? ex.GetBaseException().Message : ex.Message;
                
                // Log failed creation
                _auditService.LogAction(User.Identity.Name, "CREATE", "Positions", "new", 
                    wasSuccessful: false, errorMessage: msg);
                
                ModelState.AddModelError("", "Unable to save position: " + msg);
                ViewBag.DepartmentId = new SelectList(_uow.Departments.GetAll(), "Id", "Name", model.DepartmentId);
                ViewBag.QuestionList = _uow.Questions.GetAll(q => q.QuestionOptions).Where(q => q.IsActive).ToList();
                Debug.WriteLine("[PositionsController.Create][POST] Returning view due to exception.");
                return View(model);
            }

            // Link selected questions to this position
            if (selectedQuestions != null && selectedQuestions.Length > 0)
            {
                int order = 1;
                foreach (var qid in selectedQuestions)
                {
                    var pq = new PositionQuestion
                    {
                        PositionId = model.Id,
                        QuestionId = qid,
                        Order = order++
                    };
                    _uow.PositionQuestions.Add(pq);
                }
                _uow.Complete();
                Debug.WriteLine("[PositionsController.Create][POST] Linked " + selectedQuestions.Length + " questions.");
                
                // Log question linking
                _auditService.LogAction(User.Identity.Name, "LINK_QUESTIONS", "Positions", model.Id.ToString(), 
                    new { QuestionIds = selectedQuestions, QuestionCount = selectedQuestions.Length });
            }

            TempData["Message"] = "Position created successfully.";
            Debug.WriteLine("[PositionsController.Create][POST] Redirecting to Index.");
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Edit(int id)
        {
            var position = _uow.Positions.GetAll(p => p.PositionQuestions).FirstOrDefault(p => p.Id == id);
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

            var departmentList = _uow.Departments.GetAll().AsQueryable();
            departmentList = _tenantService.ApplyTenantFilter(departmentList);
            ViewBag.DepartmentId = new SelectList(departmentList.ToList(), "Id", "Name", position.DepartmentId);
            
            // Load questions filtered by tenant
            var allQuestions = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
            allQuestions = _tenantService.ApplyTenantFilter(allQuestions);
            ViewBag.QuestionList = allQuestions.ToList();
            Debug.WriteLine(string.Format("[PositionsController.Edit] Loaded {0} questions from database.", allQuestions.Count()));
            
            // Debug: Check each question and its options
            foreach (var q in allQuestions)
            {
                Debug.WriteLine(string.Format("Question: {0} (Type: {1})", q.Text, q.Type));
                Debug.WriteLine(string.Format("Options count: {0}", q.QuestionOptions != null ? q.QuestionOptions.Count() : 0));
                if (q.QuestionOptions != null)
                {
                    foreach (var opt in q.QuestionOptions)
                    {
                        Debug.WriteLine(string.Format("  - Option: {0} (Points: {1})", opt.Text, opt.Points));
                    }
                }
            }
            
            // Also check if there are any QuestionOptions in the database at all
            var allOptions = _uow.Context.Set<QuestionOption>().ToList();
            Debug.WriteLine(string.Format("[PositionsController.Edit] Total QuestionOptions in database: {0}", allOptions.Count));
            foreach (var opt in allOptions.Take(5))
            {
                Debug.WriteLine(string.Format("  - Option ID {0}: {1} (QuestionId: {2})", opt.Id, opt.Text, opt.QuestionId));
            }
            
            // Get currently selected question IDs for pre-checking
            var selectedQuestionIds = position.PositionQuestions != null ? position.PositionQuestions.Select(pq => pq.QuestionId).ToList() : new System.Collections.Generic.List<int>();
            ViewBag.SelectedQuestionIds = selectedQuestionIds;
            
            return View(position);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Edit(Position model, int[] selectedQuestions)
        {
            // Assign company before validation check since it's [Required] in the model
            var currentCompanyId = _tenantService.GetCurrentUserCompanyId();
            if (currentCompanyId.HasValue)
            {
                model.CompanyId = currentCompanyId.Value;
                // Clear the error if it was added due to missing form field
                if (ModelState.ContainsKey("CompanyId"))
                {
                    ModelState["CompanyId"].Errors.Clear();
                }
            }

            // ensure a department was selected
            if (model.DepartmentId <= 0)
            {
                ModelState.AddModelError("DepartmentId", "Please select a department.");
            }
            else
            {
                // Clear any default validation errors for DepartmentId if we have a valid value
                if (ModelState.ContainsKey("DepartmentId"))
                {
                    ModelState["DepartmentId"].Errors.Clear();
                }
            }

            Debug.WriteLine(string.Format("[PositionsController.Edit][POST] Title='{0}', DeptId={1}, CompanyId={2}", 
                model != null ? model.Title : "", 
                model != null ? model.DepartmentId : 0,
                model != null ? model.CompanyId : 0));
            Debug.WriteLine("ModelState.IsValid = " + ModelState.IsValid);

            if (!ModelState.IsValid)
            {
                foreach (var kvp in ModelState)
                {
                    foreach (var err in kvp.Value.Errors)
                    {
                        Debug.WriteLine(string.Format("[PositionsController.Edit][ModelError] Key='{0}', Error='{1}', Exception='{2}'", kvp.Key, err.ErrorMessage, err.Exception != null ? err.Exception.Message : ""));
                    }
                }
                ViewBag.DepartmentId = new SelectList(_uow.Departments.GetAll(), "Id", "Name", model.DepartmentId);
                var allQuestions = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
                allQuestions = _tenantService.ApplyTenantFilter(allQuestions);
                ViewBag.QuestionList = allQuestions.ToList();
                var selectedQuestionIds = selectedQuestions != null ? selectedQuestions.ToList() : new System.Collections.Generic.List<int>();
                ViewBag.SelectedQuestionIds = selectedQuestionIds;
                Debug.WriteLine("[PositionsController.Edit][POST] Returning view due to invalid ModelState.");
                return View(model);
            }

            try
            {
                // Get the existing position to preserve PostedOn and other fields not in the form
                var existingPosition = _uow.Positions.Get(model.Id);
                if (existingPosition == null)
                {
                    return HttpNotFound();
                }

                // Check tenant access for update
                var companyId = _tenantService.GetCurrentUserCompanyId();
                if (companyId.HasValue && existingPosition.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
                {
                    return new HttpStatusCodeResult(403, "Access Denied");
                }

                // Update the fields from the model
                existingPosition.Title = model.Title;
                existingPosition.Description = model.Description;
                existingPosition.Responsibilities = model.Responsibilities;
                existingPosition.Qualifications = model.Qualifications;
                existingPosition.SalaryMin = model.SalaryMin;
                existingPosition.SalaryMax = model.SalaryMax;
                existingPosition.DepartmentId = model.DepartmentId;
                existingPosition.IsOpen = model.IsOpen;
                // Update currency, default to KES if not provided
                if (!string.IsNullOrEmpty(model.Currency))
                {
                    existingPosition.Currency = model.Currency;
                }
                else if (string.IsNullOrEmpty(existingPosition.Currency))
                {
                    existingPosition.Currency = "KES";
                }

                Debug.WriteLine("[PositionsController.Edit][POST] Updating position and saving...");
                _uow.Positions.Update(existingPosition);
                _uow.Complete();
                Debug.WriteLine("[PositionsController.Edit][POST] Save succeeded.");

                // Update selected questions for this position
                // First, get existing PositionQuestions
                var existingPositionQuestions = _uow.PositionQuestions.GetAll()
                    .Where(pq => pq.PositionId == model.Id)
                    .ToList();

                // Get selected question IDs (empty array if none selected)
                var selectedQuestionIds = selectedQuestions != null ? selectedQuestions.ToList() : new System.Collections.Generic.List<int>();

                // Remove PositionQuestions that are no longer selected
                foreach (var existingPq in existingPositionQuestions)
                {
                    if (!selectedQuestionIds.Contains(existingPq.QuestionId))
                    {
                        _uow.PositionQuestions.Remove(existingPq);
                    }
                }

                // Get currently assigned question IDs
                var currentlyAssignedQuestionIds = existingPositionQuestions.Select(pq => pq.QuestionId).ToList();

                // Add new PositionQuestions for newly selected questions
                int maxOrder = existingPositionQuestions.Any() ? existingPositionQuestions.Max(pq => pq.Order) : 0;
                foreach (var questionId in selectedQuestionIds)
                {
                    if (!currentlyAssignedQuestionIds.Contains(questionId))
                    {
                        var newPq = new PositionQuestion
                        {
                            PositionId = model.Id,
                            QuestionId = questionId,
                            Order = ++maxOrder
                        };
                        _uow.PositionQuestions.Add(newPq);
                    }
                }

                _uow.Complete();
                Debug.WriteLine("[PositionsController.Edit][POST] Updated position questions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PositionsController.Edit][POST] Exception during save: " + ex);
                var msg = ex.GetBaseException() != null ? ex.GetBaseException().Message : ex.Message;
                ModelState.AddModelError("", "Unable to save position: " + msg);
                ViewBag.DepartmentId = new SelectList(_uow.Departments.GetAll(), "Id", "Name", model.DepartmentId);
                ViewBag.QuestionList = _uow.Questions.GetAll(q => q.QuestionOptions).Where(q => q.IsActive).ToList();
                var selectedQuestionIds = selectedQuestions != null ? selectedQuestions.ToList() : new System.Collections.Generic.List<int>();
                ViewBag.SelectedQuestionIds = selectedQuestionIds;
                Debug.WriteLine("[PositionsController.Edit][POST] Returning view due to exception.");
                return View(model);
            }

            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult DatabaseTest()
        {
            return View();
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
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

        [Authorize(Roles = "Admin, SuperAdmin")]
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

        [Authorize(Roles = "Admin, SuperAdmin")]
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

        [Authorize(Roles = "Admin, SuperAdmin")]
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
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult DeleteConfirmed(int id)
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

            try
            {
                // Use the context directly for more control over deletion order
                var context = _uow.Context;
                
                // Debug: Check what applications exist for this position
                var applications = context.Applications.Where(a => a.PositionId == id).ToList();
                var applicationIds = applications.Select(a => a.Id).ToList();
                
                // Debug: Log what we found
                System.Diagnostics.Debug.WriteLine(string.Format("Found {0} applications for position {1}", applications.Count, id));
                foreach (var app in applications)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Application ID: {0}, Applicant: {1}", app.Id, app.ApplicantId));
                }
                
                // Step 1: Delete PositionQuestions first
                var positionQuestions = context.PositionQuestions.Where(pq => pq.PositionId == id).ToList();
                
                // Delete the PositionQuestions themselves
                context.PositionQuestions.RemoveRange(positionQuestions);
                
                // Save changes for PositionQuestions first
                _uow.Complete();
                
                // Step 2: Delete ALL application-related entities in the correct order
                
                // Delete ApplicationAnswers for these applications
                var applicationAnswers = context.ApplicationAnswers.Where(aa => applicationIds.Contains(aa.ApplicationId));
                context.ApplicationAnswers.RemoveRange(applicationAnswers);
                
                // Delete Interviews for these applications
                var interviews = context.Interviews.Where(i => applicationIds.Contains(i.ApplicationId));
                context.Interviews.RemoveRange(interviews);
                
                // Delete Onboardings for these applications
                var onboardings = context.Onboardings.Where(o => applicationIds.Contains(o.ApplicationId));
                context.Onboardings.RemoveRange(onboardings);
                
                // Save changes for application-related entities
                _uow.Complete();
                
                // Step 3: Delete the applications themselves
                context.Applications.RemoveRange(applications);
                _uow.Complete();
                
                // Debug: Verify applications are deleted
                var remainingApps = context.Applications.Where(a => a.PositionId == id).ToList();
                System.Diagnostics.Debug.WriteLine(string.Format("Remaining applications after deletion: {0}", remainingApps.Count));
                
                // Step 4: Finally delete the position
                context.Positions.Remove(position);
                _uow.Complete();

                // Log the deletion
                var username = User.Identity.Name;
                _auditService.LogAction(username, "DELETE_POSITION", "Position", id.ToString(), 
                    string.Format("Position '{0}' and {1} associated applications deleted", position.Title, applications.Count));

                TempData["SuccessMessage"] = string.Format("Position '{0}' and {1} associated applications have been deleted successfully.", position.Title, applications.Count);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Log the error
                var username = User.Identity.Name;
                _auditService.LogAction(username, "DELETE_POSITION_ERROR", "Position", id.ToString(), 
                    string.Format("Error deleting position: {0}", ex.Message));

                ModelState.AddModelError("", "Unable to delete position. Please ensure there are no related records preventing deletion.");
                return View(position);
            }
        }
        [HttpPost]
        [Authorize]
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










