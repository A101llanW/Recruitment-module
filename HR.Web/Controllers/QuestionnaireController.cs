using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Data.Entity;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "Admin, HR, SuperAdmin")]
    [RoleBasedAuthorization("Admin", "HR")]
    public class QuestionnaireController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly ScoringService _scoringService = new ScoringService();
        private readonly TenantService _tenantService = new TenantService();

        /// <summary>
        /// Preview questionnaire for a position
        /// </summary>
        public ActionResult Preview(int positionId)
        {
            var position = _uow.Positions.Get(positionId);
            if (position == null) return HttpNotFound();

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var positionQuestions = _uow.Context.Set<PositionQuestion>()
                .Where(pq => pq.PositionId == positionId)
                .Include(pq => pq.Question)
                .OrderBy(pq => pq.Order)
                .ToList();

            var viewModel = new QuestionnairePreviewViewModel
            {
                Position = position,
                Questions = positionQuestions.Select(pq => new QuestionnaireItem
                {
                    QuestionId = pq.Question.Id,
                    QuestionText = pq.Question.Text,
                    QuestionType = pq.Question.Type,
                    Order = pq.Order,
                    Options = GetQuestionOptions(pq.Question.Id, positionId),
                    IsRequired = true
                }).ToList()
            };

            return View(viewModel);
        }



        /// <summary>
        /// Interactive questionnaire builder
        /// </summary>
        public ActionResult Builder(int positionId)
        {
            var position = _uow.Positions.Get(positionId);
            if (position == null) return HttpNotFound();

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var allQuestionsQuery = _uow.Questions.GetAll(q => q.QuestionOptions).Where(q => q.IsActive).AsQueryable();
            allQuestionsQuery = _tenantService.ApplyTenantFilter(allQuestionsQuery);
            var allQuestions = allQuestionsQuery.ToList();
            var assignedQuestions = _uow.Context.Set<PositionQuestion>()
                .Where(pq => pq.PositionId == positionId)
                .Include(pq => pq.Question)
                .OrderBy(pq => pq.Order)
                .ToList();

            var viewModel = new QuestionnaireBuilderViewModel
            {
                Position = position,
                AvailableQuestions = allQuestions,
                AssignedQuestions = assignedQuestions,
                QuestionCategories = GetQuestionCategories(),
                QuestionTypes = GetQuestionTypes()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Save questionnaire from builder
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveBuilder(QuestionnaireBuilderViewModel model)
        {
            // Verify position ownership
            var position = _uow.Positions.Get(model.Position.Id);
            if (position == null) return HttpNotFound();
            
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            try
            {
                // Remove existing assignments
                var existingAssignments = _uow.Context.Set<PositionQuestion>()
                    .Where(pq => pq.PositionId == model.Position.Id);
                _uow.Context.Set<PositionQuestion>().RemoveRange(existingAssignments);

                // Add new assignments
                var normalizedWeights = BuildEqualWeights(model.SelectedQuestionIds.Count);
                for (int i = 0; i < model.SelectedQuestionIds.Count; i++)
                {
                    var positionQuestion = new PositionQuestion
                    {
                        PositionId = model.Position.Id,
                        QuestionId = model.SelectedQuestionIds[i],
                        Order = i + 1,
                        Weight = normalizedWeights[i]
                    };
                    _uow.Context.Set<PositionQuestion>().Add(positionQuestion);
                }

                _uow.Complete();
                TempData["Message"] = "Questionnaire saved successfully!";
                return RedirectToAction("Preview", new { positionId = model.Position.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error saving questionnaire: " + ex.Message);
                return View("Builder", model);
            }
        }

        /// <summary>
        /// Get question options for a position
        /// </summary>
        private List<QuestionOptionDisplay> GetQuestionOptions(int questionId, int positionId)
        {
            var options = _uow.Context.Set<HR.Web.Models.QuestionOption>()
                .Where(qo => qo.QuestionId == questionId)
                .ToList();

            // Check for position-specific overrides
            var positionQuestion = _uow.Context.Set<PositionQuestion>()
                .FirstOrDefault(pq => pq.PositionId == positionId && pq.QuestionId == questionId);

            if (positionQuestion != null)
            {
                var positionOptions = _uow.Context.Set<PositionQuestionOption>()
                    .Where(pqo => pqo.PositionQuestionId == positionQuestion.Id)
                    .Include(pqo => pqo.QuestionOption)
                    .ToList();

                return positionOptions.Select(pqo => new QuestionOptionDisplay
                {
                    Id = pqo.QuestionOption.Id,
                    Text = pqo.QuestionOption.Text,
                    Points = pqo.Points ?? pqo.QuestionOption.Points
                }).ToList();
            }

            return options.Select(o => new QuestionOptionDisplay
            {
                Id = o.Id,
                Text = o.Text,
                Points = o.Points
            }).ToList();
        }

        private List<string> GetQuestionCategories()
        {
            return new List<string> { "technical", "behavioral", "situational", "experience", "leadership", "teamwork" };
        }

        private List<string> GetQuestionTypes()
        {
            return new List<string> { "Text", "Choice", "Number", "Rating" };
        }

        /// <summary>
        /// API endpoint for live preview
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LivePreview(int positionId, List<int> questionIds)
        {
            try
            {
                // Verify position tenant
                var position = _uow.Positions.Get(positionId);
                if (position != null)
                {
                    var companyId = _tenantService.GetCurrentUserCompanyId();
                    if (companyId.HasValue && position.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
                    {
                        return new HttpStatusCodeResult(403, "Access Denied");
                    }
                }

                var questionsQuery = _uow.Questions.GetAll(q => q.QuestionOptions)
                    .Where(q => questionIds.Contains(q.Id))
                    .AsQueryable();
                    
                questionsQuery = _tenantService.ApplyTenantFilter(questionsQuery);
                var questions = questionsQuery.ToList();

                var preview = questions.Select(q => new
                {
                    id = q.Id,
                    text = q.Text,
                    type = q.Type,
                    options = GetQuestionOptions(q.Id, positionId)
                }).ToList();

                return Json(new { success = true, preview });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Clone questionnaire from one position to another
        /// </summary>
        [HttpPost]
        public ActionResult CloneQuestionnaire(int sourcePositionId, int targetPositionId)
        {
            try
            {
                var sourcePosition = _uow.Positions.Get(sourcePositionId);
                var targetPosition = _uow.Positions.Get(targetPositionId);
                if (sourcePosition == null || targetPosition == null)
                {
                    return HttpNotFound();
                }

                var tenantAccessResult = EnsureCloneQuestionnaireTenantAccess(sourcePosition, targetPosition);
                if (tenantAccessResult != null)
                {
                    return tenantAccessResult;
                }

                var sourceQuestions = _uow.Context.Set<PositionQuestion>()
                    .Where(pq => pq.PositionId == sourcePositionId)
                    .OrderBy(pq => pq.Order)
                    .ToList();

                var existingTargetAssignments = _uow.Context.Set<PositionQuestion>()
                    .Where(pq => pq.PositionId == targetPositionId);
                _uow.Context.Set<PositionQuestion>().RemoveRange(existingTargetAssignments);

                CloneQuestionAssignments(sourceQuestions, targetPositionId);

                _uow.Complete();
                return Json(new { success = true, message = "Questionnaire cloned successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private ActionResult EnsureCloneQuestionnaireTenantAccess(Position sourcePosition, Position targetPosition)
        {
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (!companyId.HasValue || _tenantService.IsSuperAdmin())
            {
                return null;
            }

            if (sourcePosition.CompanyId == companyId.Value && targetPosition.CompanyId == companyId.Value)
            {
                return null;
            }

            return new HttpStatusCodeResult(403, "Access Denied");
        }

        private void CloneQuestionAssignments(IList<PositionQuestion> sourceQuestions, int targetPositionId)
        {
            for (var i = 0; i < sourceQuestions.Count; i++)
            {
                var positionQuestion = new PositionQuestion
                {
                    PositionId = targetPositionId,
                    QuestionId = sourceQuestions[i].QuestionId,
                    Order = i + 1,
                    Weight = sourceQuestions[i].Weight
                };
                _uow.Context.Set<PositionQuestion>().Add(positionQuestion);
            }
        }

        private static List<decimal> BuildEqualWeights(int questionCount)
        {
            var weights = new List<decimal>();
            if (questionCount <= 0)
            {
                return weights;
            }

            if (questionCount == 1)
            {
                weights.Add(100m);
                return weights;
            }

            var equalWeight = Math.Round(100m / questionCount, 2, MidpointRounding.AwayFromZero);
            for (var i = 0; i < questionCount; i++)
            {
                weights.Add(equalWeight);
            }

            var difference = 100m - weights.Sum();
            weights[weights.Count - 1] += difference;

            return weights;
        }
    }

    // View Models
    public class QuestionnairePreviewViewModel
    {
        public Position Position { get; set; }
        public List<QuestionnaireItem> Questions { get; set; }
    }

    public class QuestionnaireItem
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string QuestionType { get; set; }
        public int Order { get; set; }
        public List<QuestionOptionDisplay> Options { get; set; }
        public bool IsRequired { get; set; }
    }

    public class QuestionOptionDisplay
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public decimal Points { get; set; }
    }

    public class QuestionnaireTestViewModel
    {
        public Position Position { get; set; }
        public List<QuestionnaireTestItem> Questions { get; set; }
    }

    public class QuestionnaireTestItem
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string QuestionType { get; set; }
        public int Order { get; set; }
        public List<QuestionOptionDisplay> Options { get; set; }
        public string Answer { get; set; }
        public bool IsRequired { get; set; }
    }

    public class QuestionnaireTestResult
    {
        public Position Position { get; set; }
        public decimal TotalScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
        public List<TestQuestionResult> QuestionResults { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    public class TestQuestionResult
    {
        public string QuestionText { get; set; }
        public string Answer { get; set; }
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
    }

    public class QuestionnaireBuilderViewModel
    {
        public Position Position { get; set; }
        public List<Question> AvailableQuestions { get; set; }
        public List<PositionQuestion> AssignedQuestions { get; set; }
        public List<string> QuestionCategories { get; set; }
        public List<string> QuestionTypes { get; set; }
        public List<int> SelectedQuestionIds { get; set; }

        public QuestionnaireBuilderViewModel()
        {
            SelectedQuestionIds = new List<int>();
        }
    }
}
