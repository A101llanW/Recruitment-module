using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Models;

namespace HR.Web.Controllers
{
    public partial class PositionsController
    {
        private ActionResult HandleCreatePosition(Position model, int[] selectedQuestions)
        {
            if (_tenantService.IsSuperAdmin())
            {
                return RedirectToAction("Index");
            }

            PreparePositionModelForSave(model);
            LogPositionFormState("Create", model);

            if (!ModelState.IsValid)
            {
                return ReturnCreateValidationFailure(model);
            }

            model.PostedOn = DateTime.UtcNow;
            EnsurePositionCurrency(model);

            var saveResult = TrySaveNewPosition(model);
            if (saveResult != null)
            {
                return saveResult;
            }

            LinkSelectedQuestionsToPosition(model.Id, selectedQuestions);
            TempData["Message"] = "Position created successfully.";
            Debug.WriteLine("[PositionsController.Create][POST] Redirecting to Index.");
            return RedirectToAction("Index");
        }

        private void PreparePositionModelForSave(Position model)
        {
            AssignPositionCompany(model);
            ValidatePositionDepartment(model);
        }

        private void AssignPositionCompany(Position model)
        {
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (!companyId.HasValue)
            {
                return;
            }

            model.CompanyId = companyId.Value;
            ClearModelStateErrors("CompanyId");
        }

        private void ValidatePositionDepartment(Position model)
        {
            if (model.DepartmentId <= 0)
            {
                ModelState.AddModelError("DepartmentId", "Please select a department.");
                return;
            }

            ClearModelStateErrors("DepartmentId");
        }

        private void ClearModelStateErrors(string key)
        {
            if (ModelState.ContainsKey(key))
            {
                ModelState[key].Errors.Clear();
            }
        }

        private static void EnsurePositionCurrency(Position model)
        {
            if (string.IsNullOrEmpty(model.Currency))
            {
                model.Currency = "KES";
            }
        }

        private void LogPositionFormState(string actionName, Position model)
        {
            Debug.WriteLine(
                string.Format(
                    "[PositionsController.{0}][POST] Title='{1}', DeptId={2}, CompanyId={3}",
                    actionName,
                    model != null ? model.Title : string.Empty,
                    model != null ? model.DepartmentId : 0,
                    model != null ? model.CompanyId : 0));
            Debug.WriteLine("ModelState.IsValid = " + ModelState.IsValid);
        }

        private ActionResult ReturnCreateValidationFailure(Position model)
        {
            LogModelStateErrors("Create");
            LoadPositionFormLookups(model.DepartmentId, null);
            Debug.WriteLine("[PositionsController.Create][POST] Returning view due to invalid ModelState.");
            return View("Create", model);
        }

        private void LogModelStateErrors(string actionName)
        {
            foreach (var kvp in ModelState)
            {
                foreach (var err in kvp.Value.Errors)
                {
                    Debug.WriteLine(
                        string.Format(
                            "[PositionsController.{0}][ModelError] Key='{1}', Error='{2}', Exception='{3}'",
                            actionName,
                            kvp.Key,
                            err.ErrorMessage,
                            err.Exception != null ? err.Exception.Message : string.Empty));
                }
            }
        }

        private void LoadPositionFormLookups(int selectedDepartmentId, IEnumerable<int> selectedQuestionIds)
        {
            var departments = _uow.Departments.GetAll().AsQueryable();
            departments = _tenantService.ApplyTenantFilter(departments);
            ViewBag.DepartmentId = new SelectList(departments.ToList(), "Id", "Name", selectedDepartmentId);

            var allQuestions = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
            allQuestions = _tenantService.ApplyTenantFilter(allQuestions);
            ViewBag.QuestionList = allQuestions.ToList();
            ViewBag.SelectedQuestionIds = selectedQuestionIds != null ? selectedQuestionIds.ToList() : new List<int>();
        }

        private ActionResult TrySaveNewPosition(Position model)
        {
            try
            {
                Debug.WriteLine("[PositionsController.Create][POST] Adding position to UoW and saving...");
                _uow.Positions.Add(model);
                _uow.Complete();
                Debug.WriteLine("[PositionsController.Create][POST] Save succeeded. New Id=" + model.Id);

                _auditService.LogCreate(
                    User.Identity.Name,
                    "Positions",
                    model.Id.ToString(),
                    new
                    {
                        Title = model.Title,
                        Description = model.Description,
                        Responsibilities = model.Responsibilities,
                        Qualifications = model.Qualifications,
                        DepartmentId = model.DepartmentId,
                        Location = model.Location,
                        IsOpen = model.IsOpen,
                        PostedOn = model.PostedOn
                    });

                return null;
            }
            catch (Exception ex)
            {
                return ReturnCreateSaveFailure(model, ex);
            }
        }

        private ActionResult ReturnCreateSaveFailure(Position model, Exception ex)
        {
            Debug.WriteLine("[PositionsController.Create][POST] Exception during save: " + ex);
            var message = ex.GetBaseException() != null ? ex.GetBaseException().Message : ex.Message;

            _auditService.LogAction(
                User.Identity.Name,
                "CREATE",
                "Positions",
                "new",
                wasSuccessful: false,
                errorMessage: message);

            ModelState.AddModelError("", "Unable to save position: " + message);
            LoadPositionFormLookups(model.DepartmentId, null);
            Debug.WriteLine("[PositionsController.Create][POST] Returning view due to exception.");
            return View("Create", model);
        }

        private void LinkSelectedQuestionsToPosition(int positionId, int[] selectedQuestions)
        {
            if (selectedQuestions == null || selectedQuestions.Length == 0)
            {
                return;
            }

            var order = 1;
            foreach (var questionId in selectedQuestions)
            {
                _uow.PositionQuestions.Add(
                    new PositionQuestion
                    {
                        PositionId = positionId,
                        QuestionId = questionId,
                        Order = order++
                    });
            }

            _uow.Complete();
            Debug.WriteLine("[PositionsController.Create][POST] Linked " + selectedQuestions.Length + " questions.");
            _auditService.LogAction(
                User.Identity.Name,
                "LINK_QUESTIONS",
                "Positions",
                positionId.ToString(),
                new { QuestionIds = selectedQuestions, QuestionCount = selectedQuestions.Length });
        }

        private ActionResult HandleEditPosition(Position model, int[] selectedQuestions)
        {
            PreparePositionModelForSave(model);
            LogPositionFormState("Edit", model);

            if (!ModelState.IsValid)
            {
                return ReturnEditValidationFailure(model, selectedQuestions);
            }

            try
            {
                var existingPosition = _uow.Positions.Get(model.Id);
                if (existingPosition == null)
                {
                    return HttpNotFound();
                }

                var tenantResult = EnsurePositionTenantAccess(existingPosition);
                if (tenantResult != null)
                {
                    return tenantResult;
                }

                ApplyPositionUpdates(existingPosition, model);
                PersistPositionUpdates(existingPosition, model.Id);
                SyncPositionQuestions(model.Id, selectedQuestions);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return ReturnEditSaveFailure(model, selectedQuestions, ex);
            }
        }

        private ActionResult ReturnEditValidationFailure(Position model, int[] selectedQuestions)
        {
            LogModelStateErrors("Edit");
            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(model.DepartmentId, selectedIds);
            Debug.WriteLine("[PositionsController.Edit][POST] Returning view due to invalid ModelState.");
            return View("Edit", model);
        }

        private ActionResult EnsurePositionTenantAccess(Position position)
        {
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return null;
        }

        private static void ApplyPositionUpdates(Position existingPosition, Position model)
        {
            existingPosition.Title = model.Title;
            existingPosition.Description = model.Description;
            existingPosition.Responsibilities = model.Responsibilities;
            existingPosition.Qualifications = model.Qualifications;
            existingPosition.SalaryMin = model.SalaryMin;
            existingPosition.SalaryMax = model.SalaryMax;
            existingPosition.DepartmentId = model.DepartmentId;
            existingPosition.IsOpen = model.IsOpen;
            existingPosition.Currency = !string.IsNullOrEmpty(model.Currency)
                ? model.Currency
                : string.IsNullOrEmpty(existingPosition.Currency) ? "KES" : existingPosition.Currency;
        }

        private void PersistPositionUpdates(Position existingPosition, int positionId)
        {
            Debug.WriteLine("[PositionsController.Edit][POST] Updating position and saving...");
            _uow.Positions.Update(existingPosition);
            _uow.Complete();
            Debug.WriteLine("[PositionsController.Edit][POST] Save succeeded for position " + positionId);
        }

        private void SyncPositionQuestions(int positionId, int[] selectedQuestions)
        {
            var existingPositionQuestions = _uow.PositionQuestions.GetAll()
                .Where(pq => pq.PositionId == positionId)
                .ToList();

            var selectedQuestionIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            RemoveDeselectedQuestions(existingPositionQuestions, selectedQuestionIds);
            AddNewlySelectedQuestions(positionId, existingPositionQuestions, selectedQuestionIds);
            _uow.Complete();
            Debug.WriteLine("[PositionsController.Edit][POST] Updated position questions.");
        }

        private void RemoveDeselectedQuestions(IEnumerable<PositionQuestion> existingPositionQuestions, ICollection<int> selectedQuestionIds)
        {
            foreach (var existingPositionQuestion in existingPositionQuestions)
            {
                if (!selectedQuestionIds.Contains(existingPositionQuestion.QuestionId))
                {
                    _uow.PositionQuestions.Remove(existingPositionQuestion);
                }
            }
        }

        private void AddNewlySelectedQuestions(int positionId, ICollection<PositionQuestion> existingPositionQuestions, ICollection<int> selectedQuestionIds)
        {
            var currentlyAssignedIds = existingPositionQuestions.Select(pq => pq.QuestionId).ToList();
            var maxOrder = existingPositionQuestions.Any() ? existingPositionQuestions.Max(pq => pq.Order) : 0;

            foreach (var questionId in selectedQuestionIds)
            {
                if (currentlyAssignedIds.Contains(questionId))
                {
                    continue;
                }

                _uow.PositionQuestions.Add(
                    new PositionQuestion
                    {
                        PositionId = positionId,
                        QuestionId = questionId,
                        Order = ++maxOrder
                    });
            }
        }

        private ActionResult ReturnEditSaveFailure(Position model, int[] selectedQuestions, Exception ex)
        {
            Debug.WriteLine("[PositionsController.Edit][POST] Exception during save: " + ex);
            var msg = ex.GetBaseException() != null ? ex.GetBaseException().Message : ex.Message;
            ModelState.AddModelError("", "Unable to save position: " + msg);

            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(model.DepartmentId, selectedIds);
            Debug.WriteLine("[PositionsController.Edit][POST] Returning view due to exception.");
            return View("Edit", model);
        }

        private ActionResult HandleDeletePosition(int id)
        {
            var position = _uow.Positions.Get(id);
            if (position == null)
            {
                return HttpNotFound();
            }

            var tenantAccessResult = EnsurePositionTenantAccess(position);
            if (tenantAccessResult != null)
            {
                return tenantAccessResult;
            }

            try
            {
                var applications = GetPositionApplications(id);
                LogPositionDeletionApplications(id, applications);
                DeletePositionQuestions(id);
                DeleteApplicationDependencies(applications.Select(a => a.Id).ToList());
                DeleteApplications(applications);
                DeletePositionEntity(position);
                LogPositionDeletionSuccess(id, position.Title, applications.Count);
                TempData["SuccessMessage"] = string.Format(
                    "Position '{0}' and {1} associated applications have been deleted successfully.",
                    position.Title,
                    applications.Count);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                LogPositionDeletionError(id, ex);
                ModelState.AddModelError("", "Unable to delete position. Please ensure there are no related records preventing deletion.");
                return View("Delete", position);
            }
        }

        private List<Application> GetPositionApplications(int positionId)
        {
            return _uow.Context.Applications.Where(a => a.PositionId == positionId).ToList();
        }

        private static void LogPositionDeletionApplications(int positionId, IEnumerable<Application> applications)
        {
            var applicationList = applications.ToList();
            Debug.WriteLine(string.Format("Found {0} applications for position {1}", applicationList.Count, positionId));
            foreach (var app in applicationList)
            {
                Debug.WriteLine(string.Format("Application ID: {0}, Applicant: {1}", app.Id, app.ApplicantId));
            }
        }

        private void DeletePositionQuestions(int positionId)
        {
            var positionQuestions = _uow.Context.PositionQuestions.Where(pq => pq.PositionId == positionId).ToList();
            _uow.Context.PositionQuestions.RemoveRange(positionQuestions);
            _uow.Complete();
        }

        private void DeleteApplicationDependencies(ICollection<int> applicationIds)
        {
            _uow.Context.ApplicationAnswers.RemoveRange(_uow.Context.ApplicationAnswers.Where(aa => applicationIds.Contains(aa.ApplicationId)));
            _uow.Context.Interviews.RemoveRange(_uow.Context.Interviews.Where(i => applicationIds.Contains(i.ApplicationId)));
            _uow.Context.Onboardings.RemoveRange(_uow.Context.Onboardings.Where(o => applicationIds.Contains(o.ApplicationId)));
            _uow.Complete();
        }

        private void DeleteApplications(IEnumerable<Application> applications)
        {
            var applicationList = applications.ToList();
            _uow.Context.Applications.RemoveRange(applicationList);
            _uow.Complete();
        }

        private void DeletePositionEntity(Position position)
        {
            _uow.Context.Positions.Remove(position);
            _uow.Complete();

            var remainingApps = _uow.Context.Applications.Where(a => a.PositionId == position.Id).ToList();
            Debug.WriteLine(string.Format("Remaining applications after deletion: {0}", remainingApps.Count));
        }

        private void LogPositionDeletionSuccess(int positionId, string positionTitle, int applicationCount)
        {
            _auditService.LogAction(
                User.Identity.Name,
                "DELETE_POSITION",
                "Position",
                positionId.ToString(),
                string.Format("Position '{0}' and {1} associated applications deleted", positionTitle, applicationCount));
        }

        private void LogPositionDeletionError(int positionId, Exception ex)
        {
            _auditService.LogAction(
                User.Identity.Name,
                "DELETE_POSITION_ERROR",
                "Position",
                positionId.ToString(),
                string.Format("Error deleting position: {0}", ex.Message));
        }
    }
}
