using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        public ActionResult PositionQuestions(int positionId)
        {
            var position = _uow.Positions.Get(positionId);
            if (position == null)
            {
                return HttpNotFound();
            }

            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var availableQuestionsQuery = _uow.Questions.GetAll()
                .Where(q => q.IsActive)
                .AsQueryable();
            availableQuestionsQuery = _tenantService.ApplyTenantFilter(availableQuestionsQuery);
            var availableQuestions = availableQuestionsQuery
                .OrderBy(q => q.Text)
                .ToList();

            var assignedQuestions = _uow.Context.Set<PositionQuestion>()
                .Where(pq => pq.PositionId == positionId)
                .Include(pq => pq.Question)
                .OrderBy(pq => pq.Order)
                .ToList();

            ApplyDisplayWeights(assignedQuestions);

            var viewModel = new PositionQuestionViewModel
            {
                Position = position,
                AvailableQuestions = availableQuestions,
                AssignedQuestions = assignedQuestions,
                PositionId = positionId
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SavePositionQuestions(int positionId, List<PositionQuestionAssignmentInput> assignments)
        {
            var position = _uow.Positions.Get(positionId);
            if (position == null)
            {
                return Json(new { success = false, message = "Position not found." });
            }

            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            try
            {
                var normalizedAssignments = NormalizeAssignments(assignments);

                var incomingQuestionIds = normalizedAssignments
                    .Select(a => a.QuestionId)
                    .Distinct()
                    .ToList();

                var validQuestionQuery = _uow.Questions.GetAll()
                    .Where(q => incomingQuestionIds.Contains(q.Id))
                    .AsQueryable();
                validQuestionQuery = _tenantService.ApplyTenantFilter(validQuestionQuery);
                var validQuestionIds = new HashSet<int>(validQuestionQuery.Select(q => q.Id).ToList());

                if (incomingQuestionIds.Any(id => !validQuestionIds.Contains(id)))
                {
                    return Json(new { success = false, message = "One or more selected questions are invalid." });
                }

                var existingAssignments = _uow.Context.Set<PositionQuestion>()
                    .Where(pq => pq.PositionId == positionId)
                    .ToList();

                var incomingIdSet = new HashSet<int>(incomingQuestionIds);
                var assignmentsToRemove = existingAssignments
                    .Where(pq => !incomingIdSet.Contains(pq.QuestionId))
                    .ToList();

                if (assignmentsToRemove.Any())
                {
                    var removeIds = assignmentsToRemove.Select(pq => pq.Id).ToList();
                    var positionOptionOverrides = _uow.Context.Set<PositionQuestionOption>()
                        .Where(pqo => removeIds.Contains(pqo.PositionQuestionId));
                    _uow.Context.Set<PositionQuestionOption>().RemoveRange(positionOptionOverrides);
                    _uow.Context.Set<PositionQuestion>().RemoveRange(assignmentsToRemove);
                }

                var existingByQuestionId = existingAssignments
                    .Where(pq => !assignmentsToRemove.Any(r => r.Id == pq.Id))
                    .ToDictionary(pq => pq.QuestionId, pq => pq);

                for (var i = 0; i < normalizedAssignments.Count; i++)
                {
                    var assignment = normalizedAssignments[i];
                    PositionQuestion positionQuestion;
                    if (existingByQuestionId.TryGetValue(assignment.QuestionId, out positionQuestion))
                    {
                        positionQuestion.Order = i + 1;
                        positionQuestion.Weight = assignment.Weight;
                        positionQuestion.IsRequired = true;
                    }
                    else
                    {
                        positionQuestion = new PositionQuestion
                        {
                            PositionId = positionId,
                            QuestionId = assignment.QuestionId,
                            Order = i + 1,
                            Weight = assignment.Weight,
                            IsRequired = true
                        };
                        _uow.Context.Set<PositionQuestion>().Add(positionQuestion);
                    }
                }

                _uow.Complete();
                return Json(new { success = true, message = "Question assignments saved successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving assignments: " + ex.Message });
            }
        }

        private static void ApplyDisplayWeights(ICollection<PositionQuestion> assignedQuestions)
        {
            if (assignedQuestions == null || assignedQuestions.Count == 0)
            {
                return;
            }

            var normalizedWeights = NormalizeWeights(assignedQuestions.Select(q => q.Weight).ToList());
            var index = 0;
            foreach (var question in assignedQuestions.OrderBy(q => q.Order))
            {
                question.Weight = normalizedWeights[index++];
            }
        }

        private static List<PositionQuestionAssignmentInput> NormalizeAssignments(IEnumerable<PositionQuestionAssignmentInput> assignments)
        {
            var orderedAssignments = (assignments ?? Enumerable.Empty<PositionQuestionAssignmentInput>())
                .Where(a => a != null && a.QuestionId > 0)
                .OrderBy(a => a.Order <= 0 ? int.MaxValue : a.Order)
                .ThenBy(a => a.QuestionId)
                .GroupBy(a => a.QuestionId)
                .Select(g => g.First())
                .ToList();

            if (!orderedAssignments.Any())
            {
                return new List<PositionQuestionAssignmentInput>();
            }

            var normalizedWeights = NormalizeWeights(orderedAssignments.Select(a => a.Weight).ToList());
            for (var i = 0; i < orderedAssignments.Count; i++)
            {
                orderedAssignments[i].Order = i + 1;
                orderedAssignments[i].Weight = normalizedWeights[i];
            }

            return orderedAssignments;
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

    public class PositionQuestionAssignmentInput
    {
        public int QuestionId { get; set; }
        public int Order { get; set; }
        public decimal? Weight { get; set; }
    }
}
