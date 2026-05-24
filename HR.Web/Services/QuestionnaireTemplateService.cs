using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Services
{
    public class QuestionnaireTemplateService
    {
        private readonly UnitOfWork _uow;
        private readonly TenantService _tenantService;

        public QuestionnaireTemplateService()
            : this(new UnitOfWork(), new TenantService())
        {
        }

        public QuestionnaireTemplateService(UnitOfWork uow, TenantService tenantService)
        {
            _uow = uow ?? new UnitOfWork();
            _tenantService = tenantService ?? new TenantService();
        }

        public IList<QuestionnaireTemplateListItemViewModel> GetActiveTemplatesForCurrentTenant()
        {
            var query = _uow.QuestionnaireTemplates.GetAll(t => t.TemplateQuestions).AsQueryable();
            query = _tenantService.ApplyTenantFilter(query);
            return query
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new QuestionnaireTemplateListItemViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    QuestionCount = t.TemplateQuestions.Count,
                    StageCount = t.StageCount,
                    IsActive = t.IsActive
                })
                .ToList();
        }

        public QuestionnaireTemplate GetTemplateForEdit(int templateId)
        {
            var template = _uow.QuestionnaireTemplates
                .GetAll(t => t.TemplateQuestions)
                .FirstOrDefault(t => t.Id == templateId);

            if (template == null)
            {
                return null;
            }

            if (!CanAccessTemplate(template))
            {
                return null;
            }

            return template;
        }

        public string ValidateTemplateName(int? companyId, string name, int? excludeTemplateId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Template name is required.";
            }

            var trimmed = name.Trim();
            var query = _uow.QuestionnaireTemplates.GetAll()
                .Where(t => t.IsActive && t.Name == trimmed);

            if (companyId.HasValue)
            {
                query = query.Where(t => t.CompanyId == companyId.Value);
            }
            else
            {
                query = query.Where(t => t.CompanyId == null);
            }

            if (excludeTemplateId.HasValue)
            {
                query = query.Where(t => t.Id != excludeTemplateId.Value);
            }

            if (query.Any())
            {
                return "A template with this name already exists.";
            }

            return null;
        }

        public string SaveTemplate(
            int? templateId,
            string name,
            string description,
            int stageCount,
            IList<QuestionnaireTemplateAssignmentInput> assignments)
        {
            var companyId = ResolveTemplateCompanyId();
            var nameError = ValidateTemplateName(companyId, name, templateId);
            if (!string.IsNullOrEmpty(nameError))
            {
                return nameError;
            }

            stageCount = ClampStageCount(stageCount);
            var normalized = NormalizeTemplateAssignments(assignments);
            if (!normalized.Any())
            {
                return "Select at least one question for the template.";
            }

            var stageError = ValidateStageConfiguration(stageCount, normalized);
            if (!string.IsNullOrEmpty(stageError))
            {
                return stageError;
            }

            var questionIds = normalized.Select(a => a.QuestionId).Distinct().ToList();
            var validQuestionIds = GetValidQuestionIds(questionIds);
            if (questionIds.Any(id => !validQuestionIds.Contains(id)))
            {
                return "One or more selected questions are invalid for your company.";
            }

            QuestionnaireTemplate template;
            if (templateId.HasValue && templateId.Value > 0)
            {
                template = GetTemplateForEdit(templateId.Value);
                if (template == null)
                {
                    return "Template not found or access denied.";
                }

                template.Name = name.Trim();
                template.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                template.StageCount = stageCount;
                template.UpdatedOn = DateTime.UtcNow;

                var existingRows = _uow.Context.Set<QuestionnaireTemplateQuestion>()
                    .Where(tq => tq.TemplateId == template.Id)
                    .ToList();
                _uow.Context.Set<QuestionnaireTemplateQuestion>().RemoveRange(existingRows);
            }
            else
            {
                template = new QuestionnaireTemplate
                {
                    CompanyId = companyId,
                    Name = name.Trim(),
                    Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                    StageCount = stageCount,
                    CreatedOn = DateTime.UtcNow
                };
                _uow.QuestionnaireTemplates.Add(template);
            }

            for (var i = 0; i < normalized.Count; i++)
            {
                var assignment = normalized[i];
                _uow.QuestionnaireTemplateQuestions.Add(new QuestionnaireTemplateQuestion
                {
                    QuestionnaireTemplate = template,
                    QuestionId = assignment.QuestionId,
                    Order = i + 1,
                    Weight = assignment.Weight,
                    IsRequired = true,
                    StageNumber = assignment.StageNumber
                });
            }

            _uow.Complete();
            return null;
        }

        public string SoftDeleteTemplate(int templateId)
        {
            var template = GetTemplateForEdit(templateId);
            if (template == null)
            {
                return "Template not found or access denied.";
            }

            template.IsActive = false;
            template.UpdatedOn = DateTime.UtcNow;
            _uow.Complete();
            return null;
        }

        public object BuildTemplatePayload(int templateId)
        {
            var template = GetTemplateForEdit(templateId);
            if (template == null)
            {
                return null;
            }

            var rows = _uow.Context.Set<QuestionnaireTemplateQuestion>()
                .Where(tq => tq.TemplateId == templateId)
                .Include(tq => tq.Question)
                .OrderBy(tq => tq.Order)
                .ToList();

            return new
            {
                templateId = template.Id,
                name = template.Name,
                stageCount = template.StageCount,
                questions = rows.Select(tq => new
                {
                    questionId = tq.QuestionId,
                    text = tq.Question != null ? tq.Question.Text : string.Empty,
                    order = tq.Order,
                    weight = tq.Weight ?? 0m,
                    stageNumber = tq.StageNumber <= 0 ? 1 : tq.StageNumber
                }).ToList()
            };
        }

        public string ApplyTemplateToPosition(int templateId, int positionId, bool append = true)
        {
            var template = GetTemplateForEdit(templateId);
            if (template == null)
            {
                return "Template not found or access denied.";
            }

            var position = _uow.Positions.Get(positionId);
            if (position == null)
            {
                return "Position not found.";
            }

            if (!CanAccessPosition(position))
            {
                return "Access denied for this position.";
            }

            var templateRows = _uow.Context.Set<QuestionnaireTemplateQuestion>()
                .Where(tq => tq.TemplateId == templateId)
                .OrderBy(tq => tq.Order)
                .ToList();

            if (!templateRows.Any())
            {
                return "This template has no questions.";
            }

            if (template.StageCount > position.QuestionnaireStageCount)
            {
                position.QuestionnaireStageCount = template.StageCount;
            }

            var existing = _uow.PositionQuestions.GetAll()
                .Where(pq => pq.PositionId == positionId)
                .ToList();

            var existingQuestionIds = new HashSet<int>(existing.Select(pq => pq.QuestionId));
            var nextOrder = existing.Any() ? existing.Max(pq => pq.Order) + 1 : 1;

            foreach (var row in templateRows)
            {
                if (append && existingQuestionIds.Contains(row.QuestionId))
                {
                    continue;
                }

                if (!append)
                {
                    continue;
                }

                _uow.PositionQuestions.Add(new PositionQuestion
                {
                    PositionId = positionId,
                    QuestionId = row.QuestionId,
                    Order = nextOrder++,
                    Weight = row.Weight,
                    IsRequired = row.IsRequired,
                    StageNumber = row.StageNumber <= 0 ? 1 : row.StageNumber
                });
            }

            _uow.Complete();
            return null;
        }

        public QuestionnaireTemplateEditViewModel BuildEditViewModel(int? templateId)
        {
            var availableQuestionsQuery = _uow.Questions.GetAll(q => q.QuestionOptions, q => q.Company)
                .Where(q => q.IsActive)
                .AsQueryable();
            availableQuestionsQuery = _tenantService.ApplyTenantFilter(availableQuestionsQuery);

            var model = new QuestionnaireTemplateEditViewModel
            {
                AvailableQuestions = availableQuestionsQuery.OrderBy(q => q.Text).ToList()
            };

            if (!templateId.HasValue || templateId.Value <= 0)
            {
                return model;
            }

            var template = GetTemplateForEdit(templateId.Value);
            if (template == null)
            {
                return null;
            }

            var rows = _uow.Context.Set<QuestionnaireTemplateQuestion>()
                .Where(tq => tq.TemplateId == template.Id)
                .OrderBy(tq => tq.Order)
                .ToList();

            model.Id = template.Id;
            model.Name = template.Name;
            model.Description = template.Description;
            model.StageCount = template.StageCount;
            model.SelectedQuestionIds = rows.Select(r => r.QuestionId).ToList();
            model.SelectedQuestionWeights = rows.ToDictionary(r => r.QuestionId, r => r.Weight ?? 0m);
            model.SelectedQuestionStages = rows.ToDictionary(
                r => r.QuestionId,
                r => r.StageNumber <= 0 ? 1 : r.StageNumber);

            return model;
        }

        private int? ResolveTemplateCompanyId()
        {
            if (_tenantService.IsSuperAdmin())
            {
                return _tenantService.GetCurrentUserCompanyId();
            }

            return _tenantService.GetCurrentUserCompanyId();
        }

        private bool CanAccessTemplate(QuestionnaireTemplate template)
        {
            if (template == null)
            {
                return false;
            }

            if (_tenantService.IsSuperAdmin())
            {
                return true;
            }

            var companyId = _tenantService.GetCurrentUserCompanyId();
            return companyId.HasValue && template.CompanyId == companyId.Value;
        }

        private bool CanAccessPosition(Position position)
        {
            if (_tenantService.IsSuperAdmin())
            {
                return true;
            }

            var companyId = _tenantService.GetCurrentUserCompanyId();
            return companyId.HasValue && position.CompanyId == companyId.Value;
        }

        private HashSet<int> GetValidQuestionIds(IList<int> questionIds)
        {
            var query = _uow.Questions.GetAll()
                .Where(q => questionIds.Contains(q.Id))
                .AsQueryable();
            query = _tenantService.ApplyTenantFilter(query);
            return new HashSet<int>(query.Select(q => q.Id).ToList());
        }

        private static int ClampStageCount(int stageCount)
        {
            if (stageCount < 1)
            {
                return 1;
            }

            return stageCount > 10 ? 10 : stageCount;
        }

        private static List<QuestionnaireTemplateAssignmentInput> NormalizeTemplateAssignments(
            IEnumerable<QuestionnaireTemplateAssignmentInput> assignments)
        {
            var ordered = (assignments ?? Enumerable.Empty<QuestionnaireTemplateAssignmentInput>())
                .Where(a => a != null && a.QuestionId > 0)
                .OrderBy(a => a.Order <= 0 ? int.MaxValue : a.Order)
                .ThenBy(a => a.QuestionId)
                .GroupBy(a => a.QuestionId)
                .Select(g => g.First())
                .ToList();

            if (!ordered.Any())
            {
                return ordered;
            }

            var normalizedWeights = NormalizeWeights(ordered.Select(a => a.Weight).ToList());
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Order = i + 1;
                ordered[i].Weight = normalizedWeights[i];
                ordered[i].StageNumber = ordered[i].StageNumber <= 0 ? 1 : ordered[i].StageNumber;
            }

            return ordered;
        }

        private static string ValidateStageConfiguration(int stageCount, IList<QuestionnaireTemplateAssignmentInput> assignments)
        {
            if (stageCount <= 1)
            {
                return null;
            }

            for (var stage = 1; stage <= stageCount; stage++)
            {
                if (!assignments.Any(a => a.StageNumber == stage))
                {
                    return "Each questionnaire stage must have at least one question when using multiple stages.";
                }
            }

            return null;
        }

        private static List<decimal> NormalizeWeights(IList<decimal?> weights)
        {
            var rawInput = (weights ?? new List<decimal?>())
                .Select(w => new
                {
                    Original = w,
                    Positive = Math.Max(0m, w ?? 0m)
                })
                .ToList();

            if (!rawInput.Any())
            {
                return new List<decimal>();
            }

            if (rawInput.Count == 1)
            {
                return new List<decimal> { 100m };
            }

            List<decimal> scaled;
            var configured = rawInput.Where(w => w.Original.HasValue && w.Original.Value > 0m).ToList();
            var configuredTotal = configured.Sum(w => w.Positive);

            if (configuredTotal <= 0m)
            {
                var equalWeight = 100m / rawInput.Count;
                scaled = Enumerable.Repeat(equalWeight, rawInput.Count).ToList();
            }
            else if (configuredTotal < 100m && configured.Count < rawInput.Count)
            {
                var remainder = 100m - configuredTotal;
                var unconfiguredCount = rawInput.Count - configured.Count;
                var unconfiguredWeight = remainder / unconfiguredCount;

                scaled = new List<decimal>(rawInput.Count);
                foreach (var item in rawInput)
                {
                    if (item.Original.HasValue && item.Original.Value > 0m)
                    {
                        scaled.Add(item.Positive);
                    }
                    else
                    {
                        scaled.Add(unconfiguredWeight);
                    }
                }
            }
            else
            {
                var total = rawInput.Sum(item => item.Positive);
                scaled = rawInput
                    .Select(item => total > 0m ? (item.Positive / total) * 100m : 0m)
                    .ToList();
            }

            var rounded = scaled
                .Select(value => Math.Round(value, 2, MidpointRounding.AwayFromZero))
                .ToList();

            var difference = 100m - rounded.Sum();
            rounded[rounded.Count - 1] += difference;

            return rounded;
        }
    }
}
