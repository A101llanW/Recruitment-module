using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    public partial class PositionsController
    {
        private ActionResult HandleCreatePosition(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, string questionStagesPayload)
        {
            if (_tenantService.IsSuperAdmin())
            {
                return RedirectToAction("Index");
            }

            PreparePositionModelForSave(model);
            if (model.QuestionnaireStageCount < 1)
            {
                model.QuestionnaireStageCount = 1;
            }

            if (model.QuestionnaireStageCount > 10)
            {
                model.QuestionnaireStageCount = 10;
            }

            var stagesDict = ParseQuestionStages(questionStagesPayload, selectedQuestions, model.QuestionnaireStageCount);
            var stageConfigError = ValidateQuestionnaireStageConfiguration(model.QuestionnaireStageCount, selectedQuestions, stagesDict);
            if (!string.IsNullOrEmpty(stageConfigError))
            {
                ModelState.AddModelError("", stageConfigError);
            }

            LogPositionFormState("Create", model);

            if (!ModelState.IsValid)
            {
                return ReturnCreateValidationFailure(model, selectedQuestions, questionWeights, stagesDict);
            }

            model.PostedOn = DateTime.UtcNow;
            EnsurePositionCurrency(model);
            ApplyExpiryStatus(model);

            var saveResult = TrySaveNewPosition(model, selectedQuestions, questionWeights, stagesDict);
            if (saveResult != null)
            {
                return saveResult;
            }

            LinkSelectedQuestionsToPosition(model.Id, selectedQuestions, questionWeights, stagesDict);
            TempData["Message"] = "Position created successfully.";
            Debug.WriteLine("[PositionsController.Create][POST] Redirecting to Index.");
            return RedirectToAction("Index");
        }

        private void PreparePositionModelForSave(Position model)
        {
            NormalizeOptionalSalaryFields(model);
            AssignPositionCompany(model);
            ValidatePositionDepartment(model);
            ValidatePositionType(model);
        }

        private void NormalizeOptionalSalaryFields(Position model)
        {
            if (Request?.Form == null)
            {
                return;
            }

            var minRaw = Request.Form["SalaryMin"];
            var maxRaw = Request.Form["SalaryMax"];

            if (string.IsNullOrWhiteSpace(minRaw))
            {
                model.SalaryMin = null;
                ClearModelStateErrors("SalaryMin");
            }

            if (string.IsNullOrWhiteSpace(maxRaw))
            {
                model.SalaryMax = null;
                ClearModelStateErrors("SalaryMax");
            }
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

        private void ValidatePositionType(Position model)
        {
            if (!model.IsTechnical.HasValue)
            {
                ModelState.AddModelError("IsTechnical", "Please specify whether this role is technical or non-technical.");
                return;
            }

            ClearModelStateErrors("IsTechnical");
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

        private ActionResult ReturnCreateValidationFailure(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, int> questionStages = null)
        {
            LogModelStateErrors("Create");
            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(model.DepartmentId, selectedIds, questionWeights, questionStages);
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

        private void LoadPositionFormLookups(int selectedDepartmentId, IEnumerable<int> selectedQuestionIds, IDictionary<int, decimal> selectedQuestionWeights = null, IDictionary<int, int> selectedQuestionStages = null)
        {
            var departments = _uow.Departments.GetAll().AsQueryable();
            departments = _tenantService.ApplyTenantFilter(departments);
            ViewBag.DepartmentId = new SelectList(departments.ToList(), "Id", "Name", selectedDepartmentId);

            var allQuestions = _uow.Questions.GetAll(q => q.QuestionOptions).AsQueryable();
            allQuestions = _tenantService.ApplyTenantFilter(allQuestions);
            ViewBag.QuestionList = allQuestions.ToList();
            ViewBag.SelectedQuestionIds = selectedQuestionIds != null ? selectedQuestionIds.ToList() : new List<int>();
            ViewBag.SelectedQuestionWeights = selectedQuestionWeights != null
                ? new Dictionary<int, decimal>(selectedQuestionWeights)
                : new Dictionary<int, decimal>();
            ViewBag.SelectedQuestionStages = selectedQuestionStages != null
                ? new Dictionary<int, int>(selectedQuestionStages)
                : new Dictionary<int, int>();
            ViewBag.QuestionnaireTemplates = new QuestionnaireTemplateService().GetActiveTemplatesForCurrentTenant();
        }

        private ActionResult TrySaveNewPosition(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, int> questionStages)
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
                        PassMark = model.PassMark,
                        IsOpen = model.IsOpen,
                        PostedOn = model.PostedOn,
                        ExpiryDate = model.ExpiryDate
                    });

                return null;
            }
            catch (Exception ex)
            {
                return ReturnCreateSaveFailure(model, selectedQuestions, questionWeights, questionStages, ex);
            }
        }

        private ActionResult ReturnCreateSaveFailure(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, int> questionStages, Exception ex)
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
            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(model.DepartmentId, selectedIds, questionWeights, questionStages);
            Debug.WriteLine("[PositionsController.Create][POST] Returning view due to exception.");
            return View("Create", model);
        }

        private void LinkSelectedQuestionsToPosition(int positionId, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, int> questionStages)
        {
            if (selectedQuestions == null || selectedQuestions.Length == 0)
            {
                return;
            }

            var selectedQuestionIds = selectedQuestions.Distinct().ToList();
            var normalizedWeights = NormalizeQuestionWeights(selectedQuestionIds, questionWeights);
            var order = 1;
            foreach (var questionId in selectedQuestionIds)
            {
                decimal weight;
                if (!normalizedWeights.TryGetValue(questionId, out weight))
                {
                    weight = 0m;
                }

                int stageNumber;
                if (questionStages == null || !questionStages.TryGetValue(questionId, out stageNumber))
                {
                    stageNumber = 1;
                }

                _uow.PositionQuestions.Add(
                    new PositionQuestion
                    {
                        PositionId = positionId,
                        QuestionId = questionId,
                        Order = order++,
                        Weight = weight,
                        StageNumber = stageNumber
                    });
            }

            _uow.Complete();
            Debug.WriteLine("[PositionsController.Create][POST] Linked " + selectedQuestionIds.Count + " questions.");
            _auditService.LogAction(
                User.Identity.Name,
                "LINK_QUESTIONS",
                "Positions",
                positionId.ToString(),
                new { QuestionIds = selectedQuestionIds, QuestionCount = selectedQuestionIds.Count });
        }

        private ActionResult HandleEditPosition(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, string questionStagesPayload)
        {
            PreparePositionModelForSave(model);
            if (model.QuestionnaireStageCount < 1)
            {
                model.QuestionnaireStageCount = 1;
            }

            if (model.QuestionnaireStageCount > 10)
            {
                model.QuestionnaireStageCount = 10;
            }

            var stagesDict = ParseQuestionStages(questionStagesPayload, selectedQuestions, model.QuestionnaireStageCount);
            var stageConfigError = ValidateQuestionnaireStageConfiguration(model.QuestionnaireStageCount, selectedQuestions, stagesDict);
            if (!string.IsNullOrEmpty(stageConfigError))
            {
                ModelState.AddModelError("", stageConfigError);
            }

            LogPositionFormState("Edit", model);

            if (!ModelState.IsValid)
            {
                return ReturnEditValidationFailure(model, selectedQuestions, questionWeights, stagesDict);
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
                SyncPositionQuestions(model.Id, selectedQuestions, questionWeights, stagesDict);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return ReturnEditSaveFailure(model, selectedQuestions, questionWeights, stagesDict, ex);
            }
        }

        private ActionResult ReturnEditValidationFailure(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, int> questionStages = null)
        {
            LogModelStateErrors("Edit");
            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(model.DepartmentId, selectedIds, questionWeights, questionStages);
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

        private void ApplyPositionUpdates(Position existingPosition, Position model)
        {
            existingPosition.Title = model.Title;
            existingPosition.Description = model.Description;
            existingPosition.Responsibilities = model.Responsibilities;
            existingPosition.Qualifications = model.Qualifications;
            existingPosition.IsTechnical = model.IsTechnical;
            existingPosition.SalaryMin = model.SalaryMin;
            existingPosition.SalaryMax = model.SalaryMax;
            existingPosition.DepartmentId = model.DepartmentId;
            existingPosition.IsOpen = model.IsOpen;
            existingPosition.ExpiryDate = model.ExpiryDate;
            existingPosition.PassMark = model.PassMark;
            existingPosition.QuestionnaireStageCount = model.QuestionnaireStageCount;
            existingPosition.Currency = !string.IsNullOrEmpty(model.Currency)
                ? model.Currency
                : string.IsNullOrEmpty(existingPosition.Currency) ? "KES" : existingPosition.Currency;
            ApplyExpiryStatus(existingPosition);
        }

        private static void ApplyExpiryStatus(Position position)
        {
            if (position == null)
            {
                return;
            }

            if (HasReachedExpiry(position.ExpiryDate))
            {
                position.IsOpen = false;
            }
        }

        private void PersistPositionUpdates(Position existingPosition, int positionId)
        {
            Debug.WriteLine("[PositionsController.Edit][POST] Updating position and saving...");
            _uow.Positions.Update(existingPosition);
            _uow.Complete();
            Debug.WriteLine("[PositionsController.Edit][POST] Save succeeded for position " + positionId);
        }

        private void SyncPositionQuestions(int positionId, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, int> questionStages)
        {
            var existingPositionQuestions = _uow.PositionQuestions.GetAll()
                .Where(pq => pq.PositionId == positionId)
                .ToList();

            var selectedQuestionIds = selectedQuestions != null
                ? selectedQuestions.Distinct().ToList()
                : new List<int>();
            var selectedSet = new HashSet<int>(selectedQuestionIds);
            var normalizedWeights = NormalizeQuestionWeights(selectedQuestionIds, questionWeights);

            foreach (var existingPositionQuestion in existingPositionQuestions.Where(pq => !selectedSet.Contains(pq.QuestionId)).ToList())
            {
                _uow.PositionQuestions.Remove(existingPositionQuestion);
            }

            var existingByQuestionId = existingPositionQuestions
                .Where(pq => selectedSet.Contains(pq.QuestionId))
                .ToDictionary(pq => pq.QuestionId, pq => pq);

            for (var i = 0; i < selectedQuestionIds.Count; i++)
            {
                var questionId = selectedQuestionIds[i];
                PositionQuestion positionQuestion;
                if (!existingByQuestionId.TryGetValue(questionId, out positionQuestion))
                {
                    positionQuestion = new PositionQuestion
                    {
                        PositionId = positionId,
                        QuestionId = questionId
                    };
                    _uow.PositionQuestions.Add(positionQuestion);
                }

                decimal weight;
                if (!normalizedWeights.TryGetValue(questionId, out weight))
                {
                    weight = 0m;
                }

                positionQuestion.Order = i + 1;
                positionQuestion.Weight = weight;

                int stageNumber;
                if (questionStages == null || !questionStages.TryGetValue(questionId, out stageNumber))
                {
                    stageNumber = 1;
                }

                positionQuestion.StageNumber = stageNumber;
            }

            _uow.Complete();
            Debug.WriteLine("[PositionsController.Edit][POST] Updated position questions.");
        }

        private static IDictionary<int, decimal> NormalizeQuestionWeights(IList<int> selectedQuestionIds, IDictionary<int, decimal> questionWeights)
        {
            var normalized = new Dictionary<int, decimal>();
            if (selectedQuestionIds == null || selectedQuestionIds.Count == 0)
            {
                return normalized;
            }

            var providedWeights = new List<decimal>();
            foreach (var questionId in selectedQuestionIds)
            {
                decimal provided;
                if (questionWeights != null && questionWeights.TryGetValue(questionId, out provided))
                {
                    provided = Math.Max(0m, Math.Min(100m, provided));
                }
                else
                {
                    provided = 0m;
                }

                providedWeights.Add(provided);
            }

            var totalProvided = providedWeights.Sum();
            List<decimal> scaledWeights;
            if (totalProvided <= 0m)
            {
                var even = 100m / selectedQuestionIds.Count;
                scaledWeights = Enumerable.Repeat(even, selectedQuestionIds.Count).ToList();
            }
            else if (totalProvided > 100m)
            {
                // Cap the overall budget at 100 without forcing totals up when they are below 100.
                scaledWeights = providedWeights.Select(weight => (weight / totalProvided) * 100m).ToList();
            }
            else
            {
                scaledWeights = providedWeights.ToList();
            }

            var rounded = scaledWeights.Select(value => Math.Round(value, 2, MidpointRounding.AwayFromZero)).ToList();

            for (var i = 0; i < selectedQuestionIds.Count; i++)
            {
                normalized[selectedQuestionIds[i]] = rounded[i];
            }

            return normalized;
        }

        private ActionResult ReturnEditSaveFailure(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, int> questionStages, Exception ex)
        {
            Debug.WriteLine("[PositionsController.Edit][POST] Exception during save: " + ex);
            var msg = ex.GetBaseException() != null ? ex.GetBaseException().Message : ex.Message;
            ModelState.AddModelError("", "Unable to save position: " + msg);

            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(model.DepartmentId, selectedIds, questionWeights, questionStages);
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
