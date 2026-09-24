using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Data;
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

            if (model == null)
            {
                ModelState.AddModelError("", "Invalid position data.");
                return View("Create", new Position());
            }

            var positionModel = model;
            PreparePositionModelForSave(positionModel);
            if (positionModel.QuestionnaireStageCount < 1)
            {
                positionModel.QuestionnaireStageCount = 1;
            }

            if (positionModel.QuestionnaireStageCount > 10)
            {
                positionModel.QuestionnaireStageCount = 10;
            }

            var stagesDict = ParseQuestionStages(questionStagesPayload, selectedQuestions, positionModel.QuestionnaireStageCount);
            var stageConfigError = ValidateQuestionnaireStageConfiguration(positionModel.QuestionnaireStageCount, selectedQuestions, stagesDict);
            if (!string.IsNullOrEmpty(stageConfigError))
            {
                ModelState.AddModelError("", stageConfigError);
            }

            if (!ModelState.IsValid)
            {
                return ReturnCreateValidationFailure(positionModel, selectedQuestions, questionWeights, stagesDict);
            }

            positionModel.PostedOn = DateTime.UtcNow;
            EnsurePositionCurrency(positionModel);
            ApplyExpiryStatus(positionModel);

            var saveResult = TrySaveNewPosition(positionModel, selectedQuestions, questionWeights, stagesDict);
            if (saveResult != null)
            {
                return saveResult;
            }

            LinkSelectedQuestionsToPosition(positionModel.Id, selectedQuestions, questionWeights, stagesDict);
            TempData["Message"] = "Position created successfully.";
            return RedirectToAction("Index");
        }

        private void PreparePositionModelForSave(Position model)
        {
            if (model == null)
            {
                return;
            }

            var positionModel = model;
            NormalizeOptionalSalaryFields(positionModel);
            AssignPositionCompany(positionModel);
            ValidatePositionDepartment(positionModel);
            ValidatePositionType(positionModel);
            ValidatePositionTitle(positionModel);
            ValidatePositionExpiryDate(positionModel);
        }

        private void NormalizeOptionalSalaryFields(Position model)
        {
            if (model == null || Request?.Form == null)
            {
                return;
            }

            var positionModel = model;
            var minRaw = Request.Form["SalaryMin"];
            var maxRaw = Request.Form["SalaryMax"];

            if (string.IsNullOrWhiteSpace(minRaw))
            {
                positionModel.SalaryMin = null;
                ClearModelStateErrors("SalaryMin");
            }

            if (string.IsNullOrWhiteSpace(maxRaw))
            {
                positionModel.SalaryMax = null;
                ClearModelStateErrors("SalaryMax");
            }
        }

        private void AssignPositionCompany(Position model)
        {
            if (model == null)
            {
                return;
            }

            var positionModel = model;
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (!companyId.HasValue)
            {
                return;
            }

            positionModel.CompanyId = companyId.Value;
            ClearModelStateErrors("CompanyId");
        }

        private void ValidatePositionDepartment(Position model)
        {
            if (model == null)
            {
                return;
            }

            var positionModel = model;
            if (positionModel.DepartmentId <= 0)
            {
                ModelState.AddModelError("DepartmentId", "Please select a department.");
                return;
            }

            ClearModelStateErrors("DepartmentId");
        }

        private void ValidatePositionType(Position model)
        {
            if (model == null)
            {
                return;
            }

            var positionModel = model;
            if (!positionModel.IsTechnical.HasValue)
            {
                ModelState.AddModelError("IsTechnical", "Please specify whether this role is technical or non-technical.");
                return;
            }

            ClearModelStateErrors("IsTechnical");
        }

        private void ValidatePositionTitle(Position model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Title))
            {
                return;
            }

            var companyId = ResolvePositionCompanyId(model);
            if (!companyId.HasValue)
            {
                ModelState.AddModelError("Title", "Unable to determine the organization for this position.");
                return;
            }

            if (PositionTitleExistsInCompany(companyId.Value, model.Title.Trim(), model.Id))
            {
                ModelState.AddModelError("Title", "A position with this title already exists for your organization.");
                return;
            }

            ClearModelStateErrors("Title");
        }

        private int? ResolvePositionCompanyId(Position model)
        {
            if (model != null && model.CompanyId.HasValue && model.CompanyId.Value > 0)
            {
                return model.CompanyId.Value;
            }

            return _tenantService.GetCurrentUserCompanyId();
        }

        private bool PositionTitleExistsInCompany(int companyId, string normalizedTitle, int excludePositionId)
        {
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                return false;
            }

            var lowerTitle = normalizedTitle.Trim().ToLower();
            return _uow.Context.Set<Position>()
                .AsNoTracking()
                .Any(p => p.CompanyId == companyId
                    && p.Id != excludePositionId
                    && p.Title != null
                    && p.Title.ToLower() == lowerTitle);
        }

        private void ValidatePositionExpiryDate(Position model)
        {
            if (model == null || !model.ExpiryDate.HasValue)
            {
                return;
            }

            if (model.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("ExpiryDate", "Position expiry date cannot be before today's date.");
                return;
            }

            ClearModelStateErrors("ExpiryDate");
        }

        private void ClearModelStateErrors(string key)
        {
            if (!ModelState.ContainsKey(key))
            {
                return;
            }

            var entry = ModelState[key];
            if (entry != null)
            {
                entry.Errors.Clear();
            }
        }

        private static void EnsurePositionCurrency(Position model)
        {
            if (string.IsNullOrEmpty(model.Currency))
            {
                model.Currency = "KES";
            }
        }

        private ActionResult ReturnCreateValidationFailure(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, HashSet<int>> questionStages = null)
        {
            if (model == null)
            {
                return View("Create", new Position());
            }

            var positionModel = model;
            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(positionModel.DepartmentId, selectedIds, questionWeights, questionStages);
            return View("Create", positionModel);
        }

        private void LoadPositionFormLookups(int selectedDepartmentId, IEnumerable<int> selectedQuestionIds, IDictionary<int, decimal> selectedQuestionWeights = null, IDictionary<int, HashSet<int>> selectedQuestionStages = null, int positionId = 0)
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
            ViewBag.SelectedQuestionStages = QuestionStagePayloadHelper.ToOrderedLists(selectedQuestionStages);
            ViewBag.QuestionnaireTemplates = new QuestionnaireTemplateService().GetActiveTemplatesForCurrentTenant();
            ApplyQuestionnaireLockViewBag(positionId);
        }

        private void ApplyQuestionnaireLockViewBag(int positionId)
        {
            if (positionId <= 0)
            {
                ViewBag.QuestionnaireLockInfo = null;
                ViewBag.QuestionnaireLockedStages = new List<int>();
                ViewBag.QuestionnaireLockMessage = null;
                ViewBag.QuestionnaireMinStageCount = 1;
                return;
            }

            var lockInfo = PositionQuestionnaireLockHelper.GetLockInfo(_uow.Context, positionId);
            ViewBag.QuestionnaireLockInfo = lockInfo;
            ViewBag.QuestionnaireLockedStages = lockInfo.LockedStageNumbers != null
                ? lockInfo.LockedStageNumbers.OrderBy(s => s).ToList()
                : new List<int>();
            ViewBag.QuestionnaireLockMessage = lockInfo.BuildLockMessage();
            ViewBag.QuestionnaireMinStageCount = lockInfo.MinAllowedStageCount;
        }

        private ActionResult TrySaveNewPosition(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, HashSet<int>> questionStages)
        {
            if (model == null)
            {
                ModelState.AddModelError("", "Invalid position data.");
                return View("Create", new Position());
            }

            var positionModel = model;
            var companyId = ResolvePositionCompanyId(positionModel);
            if (companyId.HasValue &&
                PositionTitleExistsInCompany(companyId.Value, positionModel.Title, positionModel.Id))
            {
                ModelState.AddModelError("Title", "A position with this title already exists for your organization.");
                return ReturnCreateSaveFailure(
                    positionModel,
                    selectedQuestions,
                    questionWeights,
                    questionStages,
                    new InvalidOperationException("Duplicate position title."));
            }

            try
            {
                _uow.Positions.Add(positionModel);
                _uow.Complete();

                _auditService.LogCreate(
                    GetCurrentActorName(),
                    "Positions",
                    positionModel.Id.ToString(),
                    new
                    {
                        Title = positionModel.Title,
                        Description = positionModel.Description,
                        Responsibilities = positionModel.Responsibilities,
                        Qualifications = positionModel.Qualifications,
                        DepartmentId = positionModel.DepartmentId,
                        Location = positionModel.Location,
                        PassMark = positionModel.PassMark,
                        IsOpen = positionModel.IsOpen,
                        PostedOn = positionModel.PostedOn,
                        ExpiryDate = positionModel.ExpiryDate
                    });

                return null;
            }
            catch (Exception ex)
            {
                return ReturnCreateSaveFailure(positionModel, selectedQuestions, questionWeights, questionStages, ex);
            }
        }

        private ActionResult ReturnCreateSaveFailure(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, HashSet<int>> questionStages, Exception ex)
        {
            if (model == null)
            {
                ModelState.AddModelError("", "Unable to save position.");
                return View("Create", new Position());
            }

            var positionModel = model;
            var message = ex.GetBaseException() != null ? ex.GetBaseException().Message : ex.Message;

            _auditService.LogAction(
                GetCurrentActorName(),
                "CREATE",
                "Positions",
                "new",
                wasSuccessful: false,
                errorMessage: message);

            ModelState.AddModelError("", "Unable to save position: " + message);
            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(positionModel.DepartmentId, selectedIds, questionWeights, questionStages);
            return View("Create", positionModel);
        }

        private void LinkSelectedQuestionsToPosition(int positionId, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, HashSet<int>> questionStages)
        {
            if (selectedQuestions == null || selectedQuestions.Length == 0)
            {
                return;
            }

            var selectedQuestionIds = selectedQuestions.Distinct().ToList();
            var normalizedWeights = NormalizeQuestionWeights(selectedQuestionIds, questionWeights);
            var assignments = BuildQuestionStageAssignments(selectedQuestionIds, questionStages);
            var order = 1;
            foreach (var assignment in assignments)
            {
                decimal weight;
                if (!normalizedWeights.TryGetValue(assignment.QuestionId, out weight))
                {
                    weight = 0m;
                }

                _uow.PositionQuestions.Add(
                    new PositionQuestion
                    {
                        PositionId = positionId,
                        QuestionId = assignment.QuestionId,
                        Order = order++,
                        Weight = weight,
                        StageNumber = assignment.StageNumber
                    });
            }

            _uow.Complete();
            _auditService.LogAction(
                GetCurrentActorName(),
                "LINK_QUESTIONS",
                "Positions",
                positionId.ToString(),
                new { QuestionIds = selectedQuestionIds, QuestionCount = selectedQuestionIds.Count });
        }

        private ActionResult HandleEditPosition(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, string questionStagesPayload)
        {
            if (model == null)
            {
                return HttpNotFound();
            }

            var positionModel = model;
            PreparePositionModelForSave(positionModel);
            if (positionModel.QuestionnaireStageCount < 1)
            {
                positionModel.QuestionnaireStageCount = 1;
            }

            if (positionModel.QuestionnaireStageCount > 10)
            {
                positionModel.QuestionnaireStageCount = 10;
            }

            var stagesDict = ParseQuestionStages(questionStagesPayload, selectedQuestions, positionModel.QuestionnaireStageCount);
            var stageConfigError = ValidateQuestionnaireStageConfiguration(positionModel.QuestionnaireStageCount, selectedQuestions, stagesDict);
            if (!string.IsNullOrEmpty(stageConfigError))
            {
                ModelState.AddModelError("", stageConfigError);
            }

            try
            {
                var existingPosition = _uow.Positions.Get(positionModel.Id);
                if (existingPosition == null)
                {
                    return HttpNotFound();
                }

                var tenantResult = EnsurePositionTenantAccess(existingPosition);
                if (tenantResult != null)
                {
                    return tenantResult;
                }

                var lockInfo = PositionQuestionnaireLockHelper.GetLockInfo(_uow.Context, positionModel.Id);
                var stageCountError = PositionQuestionnaireLockHelper.ValidateStageCountChange(
                    lockInfo,
                    existingPosition.QuestionnaireStageCount,
                    positionModel.QuestionnaireStageCount);
                if (!string.IsNullOrEmpty(stageCountError))
                {
                    ModelState.AddModelError("", stageCountError);
                }

                var selectedQuestionIds = selectedQuestions != null
                    ? selectedQuestions.Distinct().ToList()
                    : new List<int>();
                var normalizedWeights = NormalizeQuestionWeights(selectedQuestionIds, questionWeights);
                var proposedAssignments = BuildQuestionStageAssignments(selectedQuestionIds, stagesDict);
                var existingPositionQuestions = _uow.PositionQuestions.GetAll()
                    .Where(pq => pq.PositionId == positionModel.Id)
                    .ToList();
                var questionSyncError = PositionQuestionnaireLockHelper.ValidateMultiStageQuestionSync(
                    lockInfo,
                    existingPositionQuestions,
                    ToQuestionStageAssignments(proposedAssignments),
                    normalizedWeights,
                    existingPosition.QuestionnaireStageCount,
                    positionModel.QuestionnaireStageCount);
                if (!string.IsNullOrEmpty(questionSyncError))
                {
                    ModelState.AddModelError("", questionSyncError);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Unable to validate questionnaire changes: " + ex.Message);
            }

            if (!ModelState.IsValid)
            {
                return ReturnEditValidationFailure(positionModel, selectedQuestions, questionWeights, stagesDict);
            }

            try
            {
                var existingPosition = _uow.Positions.Get(positionModel.Id);
                if (existingPosition == null)
                {
                    return HttpNotFound();
                }

                var tenantResult = EnsurePositionTenantAccess(existingPosition);
                if (tenantResult != null)
                {
                    return tenantResult;
                }

                ApplyPositionUpdates(existingPosition, positionModel);
                PersistPositionUpdates(existingPosition, positionModel.Id);
                SyncPositionQuestions(positionModel.Id, selectedQuestions, questionWeights, stagesDict);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return ReturnEditSaveFailure(positionModel, selectedQuestions, questionWeights, stagesDict, ex);
            }
        }

        private ActionResult ReturnEditValidationFailure(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, HashSet<int>> questionStages = null)
        {
            if (model == null)
            {
                return View("Edit", new Position());
            }

            var positionModel = model;
            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(positionModel.DepartmentId, selectedIds, questionWeights, questionStages, positionModel.Id);
            return View("Edit", positionModel);
        }

        private ActionResult EnsurePositionTenantAccess(Position position)
        {
            if (position == null)
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var scopedPosition = position;
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && scopedPosition.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return null;
        }

        private void ApplyPositionUpdates(Position existingPosition, Position model)
        {
            if (existingPosition == null || model == null)
            {
                return;
            }

            var sourceModel = model;
            existingPosition.Title = sourceModel.Title;
            existingPosition.Description = sourceModel.Description;
            existingPosition.Responsibilities = sourceModel.Responsibilities;
            existingPosition.Qualifications = sourceModel.Qualifications;
            existingPosition.IsTechnical = sourceModel.IsTechnical;
            existingPosition.SalaryMin = sourceModel.SalaryMin;
            existingPosition.SalaryMax = sourceModel.SalaryMax;
            existingPosition.DepartmentId = sourceModel.DepartmentId;
            existingPosition.IsOpen = sourceModel.IsOpen;
            existingPosition.ExpiryDate = sourceModel.ExpiryDate;
            existingPosition.PassMark = sourceModel.PassMark;
            existingPosition.QuestionnaireStageCount = sourceModel.QuestionnaireStageCount;
            existingPosition.Currency = !string.IsNullOrEmpty(sourceModel.Currency)
                ? sourceModel.Currency
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
            _uow.Positions.Update(existingPosition);
            _uow.Complete();
        }

        private void SyncPositionQuestions(int positionId, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, HashSet<int>> questionStages)
        {
            var existingPositionQuestions = _uow.PositionQuestions.GetAll()
                .Where(pq => pq.PositionId == positionId)
                .ToList();

            var selectedQuestionIds = selectedQuestions != null
                ? selectedQuestions.Distinct().ToList()
                : new List<int>();
            var selectedSet = new HashSet<int>(selectedQuestionIds);
            var normalizedWeights = NormalizeQuestionWeights(selectedQuestionIds, questionWeights);
            var assignments = BuildQuestionStageAssignments(selectedQuestionIds, questionStages);
            var desiredKeys = new HashSet<string>(
                assignments.Select(a => BuildQuestionStageKey(a.QuestionId, a.StageNumber)));

            foreach (var existingPositionQuestion in existingPositionQuestions.ToList())
            {
                var key = BuildQuestionStageKey(existingPositionQuestion.QuestionId, existingPositionQuestion.StageNumber);
                if (!selectedSet.Contains(existingPositionQuestion.QuestionId) || !desiredKeys.Contains(key))
                {
                    _uow.PositionQuestions.Remove(existingPositionQuestion);
                }
            }

            var existingByKey = existingPositionQuestions
                .Where(pq => selectedSet.Contains(pq.QuestionId))
                .ToDictionary(pq => BuildQuestionStageKey(pq.QuestionId, pq.StageNumber), pq => pq);

            for (var i = 0; i < assignments.Count; i++)
            {
                var assignment = assignments[i];
                var key = BuildQuestionStageKey(assignment.QuestionId, assignment.StageNumber);
                PositionQuestion positionQuestion;
                if (!existingByKey.TryGetValue(key, out positionQuestion))
                {
                    positionQuestion = new PositionQuestion
                    {
                        PositionId = positionId,
                        QuestionId = assignment.QuestionId,
                        StageNumber = assignment.StageNumber
                    };
                    _uow.PositionQuestions.Add(positionQuestion);
                    existingByKey[key] = positionQuestion;
                }

                decimal weight;
                if (!normalizedWeights.TryGetValue(assignment.QuestionId, out weight))
                {
                    weight = 0m;
                }

                positionQuestion.Order = i + 1;
                positionQuestion.Weight = weight;
                positionQuestion.StageNumber = assignment.StageNumber;
            }

            _uow.Complete();
        }

        private static string BuildQuestionStageKey(int questionId, int stageNumber)
        {
            return questionId + ":" + stageNumber;
        }

        private static List<PositionQuestionStageAssignment> BuildQuestionStageAssignments(
            IList<int> selectedQuestionIds,
            IDictionary<int, HashSet<int>> questionStages)
        {
            var assignments = new List<PositionQuestionStageAssignment>();
            if (selectedQuestionIds == null || selectedQuestionIds.Count == 0)
            {
                return assignments;
            }

            foreach (var questionId in selectedQuestionIds)
            {
                HashSet<int> stageSet;
                if (questionStages == null || !questionStages.TryGetValue(questionId, out stageSet) || stageSet == null || !stageSet.Any())
                {
                    stageSet = new HashSet<int> { 1 };
                }

                foreach (var stageNumber in stageSet.Where(s => s > 0).OrderBy(s => s))
                {
                    assignments.Add(new PositionQuestionStageAssignment
                    {
                        QuestionId = questionId,
                        StageNumber = stageNumber
                    });
                }
            }

            return assignments;
        }

        private sealed class PositionQuestionStageAssignment
        {
            public int QuestionId { get; set; }
            public int StageNumber { get; set; }
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

        private ActionResult ReturnEditSaveFailure(Position model, int[] selectedQuestions, IDictionary<int, decimal> questionWeights, IDictionary<int, HashSet<int>> questionStages, Exception ex)
        {
            if (model == null)
            {
                ModelState.AddModelError("", "Unable to save position.");
                return View("Edit", new Position());
            }

            var positionModel = model;
            var msg = ex.GetBaseException() != null ? ex.GetBaseException().Message : ex.Message;
            ModelState.AddModelError("", "Unable to save position: " + msg);

            var selectedIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
            LoadPositionFormLookups(positionModel.DepartmentId, selectedIds, questionWeights, questionStages, positionModel.Id);
            return View("Edit", positionModel);
        }

        private static List<QuestionStageAssignment> ToQuestionStageAssignments(IEnumerable<PositionQuestionStageAssignment> assignments)
        {
            return (assignments ?? Enumerable.Empty<PositionQuestionStageAssignment>())
                .Select(a => new QuestionStageAssignment
                {
                    QuestionId = a.QuestionId,
                    StageNumber = a.StageNumber
                })
                .ToList();
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
        }

        private void LogPositionDeletionSuccess(int positionId, string positionTitle, int applicationCount)
        {
            _auditService.LogAction(
                GetCurrentActorName(),
                "DELETE_POSITION",
                "Position",
                positionId.ToString(),
                string.Format("Position '{0}' and {1} associated applications deleted", positionTitle, applicationCount));
        }

        private void LogPositionDeletionError(int positionId, Exception ex)
        {
            _auditService.LogAction(
                GetCurrentActorName(),
                "DELETE_POSITION_ERROR",
                "Position",
                positionId.ToString(),
                string.Format("Error deleting position: {0}", ex.Message));
        }
    }
}
