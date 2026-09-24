using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HR.Web.Controllers;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Helpers
{
    public static class QuestionStagePayloadHelper
    {
        public static Dictionary<int, HashSet<int>> Parse(string payload, int[] selectedQuestions, int questionnaireStageCount)
        {
            var stages = new Dictionary<int, HashSet<int>>();
            if (selectedQuestions != null)
            {
                foreach (var questionId in selectedQuestions.Distinct())
                {
                    if (questionId > 0)
                    {
                        stages[questionId] = new HashSet<int> { 1 };
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                return stages;
            }

            var max = Math.Max(1, questionnaireStageCount);
            foreach (var entry in payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('=');
                if (parts.Length != 2)
                {
                    continue;
                }

                int questionId;
                if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out questionId) || questionId <= 0)
                {
                    continue;
                }

                if (!stages.ContainsKey(questionId))
                {
                    continue;
                }

                foreach (var stageToken in parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int stage;
                    if (!int.TryParse(stageToken.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out stage))
                    {
                        continue;
                    }

                    stage = Math.Max(1, Math.Min(max, stage));
                    stages[questionId].Add(stage);
                }
            }

            return stages;
        }

        public static string ValidateAllStagesHaveQuestions(int questionnaireStageCount, int[] selectedQuestions, IDictionary<int, HashSet<int>> stages)
        {
            if (questionnaireStageCount <= 1)
            {
                return null;
            }

            var selected = selectedQuestions != null
                ? selectedQuestions.Where(id => id > 0).Distinct().ToList()
                : new List<int>();
            if (!selected.Any())
            {
                return null;
            }

            for (var stage = 1; stage <= questionnaireStageCount; stage++)
            {
                if (!selected.Any(questionId =>
                    stages.ContainsKey(questionId) &&
                    stages[questionId] != null &&
                    stages[questionId].Contains(stage)))
                {
                    return string.Format(
                        "This position uses {0} questionnaire stages. Add at least one question assigned to stage {1}.",
                        questionnaireStageCount,
                        stage);
                }
            }

            return null;
        }

        public static Dictionary<int, IList<int>> ToOrderedLists(IDictionary<int, HashSet<int>> stages)
        {
            if (stages == null)
            {
                return new Dictionary<int, IList<int>>();
            }

            return stages.ToDictionary(
                kvp => kvp.Key,
                kvp => (IList<int>)(kvp.Value != null
                    ? kvp.Value.Where(s => s > 0).OrderBy(s => s).ToList()
                    : new List<int>()));
        }
    }

    public sealed class PositionQuestionnaireLockInfo
    {
        public bool HasApplications { get; set; }
        public int ApplicationCount { get; set; }
        public HashSet<int> LockedStageNumbers { get; set; }
        public int HighestLockedStage { get; set; }
        public int MinAllowedStageCount { get; set; }
        public bool IsFullyLocked { get; set; }

        public PositionQuestionnaireLockInfo()
        {
            LockedStageNumbers = new HashSet<int>();
            MinAllowedStageCount = 1;
        }

        public string BuildLockMessage()
        {
            if (!HasApplications)
            {
                return null;
            }

            if (LockedStageNumbers == null || LockedStageNumbers.Count == 0)
            {
                return "Candidates have applied for this position. Question assignments stay editable until a stage receives answers.";
            }

            var lockedList = LockedStageNumbers.OrderBy(s => s).ToList();
            var stageLabel = lockedList.Count == 1
                ? "stage " + lockedList[0]
                : "stages " + string.Join(", ", lockedList);

            return "You cannot edit questions for " + stageLabel
                + " because candidates have already submitted answers. "
                + "Increase the number of questionnaire stages to add questions on a new stage, then invite top applicants from the Applications screen.";
        }
    }

    public static class PositionQuestionnaireLockHelper
    {
        public static PositionQuestionnaireLockInfo GetLockInfo(HrContext context, int positionId)
        {
            if (context == null || positionId <= 0)
            {
                return new PositionQuestionnaireLockInfo();
            }

            var applicationQuery = context.Applications.Where(a => a.PositionId == positionId);
            var applicationCount = applicationQuery.Count();
            var maxCompletedStage = applicationQuery
                .Select(a => (int?)a.LastCompletedQuestionnaireStage)
                .DefaultIfEmpty(0)
                .Max() ?? 0;

            var lockedStages = context.ApplicationAnswers
                .Where(aa => aa.Application.PositionId == positionId)
                .Select(aa => aa.StageNumber <= 0 ? 1 : aa.StageNumber)
                .Distinct()
                .ToList();

            var lockedStageNumbers = new HashSet<int>(lockedStages);
            var highestLockedStage = lockedStageNumbers.Count > 0 ? lockedStageNumbers.Max() : 0;
            var minAllowedStageCount = Math.Max(1, Math.Max(highestLockedStage, maxCompletedStage));

            return new PositionQuestionnaireLockInfo
            {
                HasApplications = applicationCount > 0,
                ApplicationCount = applicationCount,
                LockedStageNumbers = lockedStageNumbers,
                HighestLockedStage = highestLockedStage,
                MinAllowedStageCount = minAllowedStageCount,
                IsFullyLocked = lockedStageNumbers.Contains(1)
            };
        }

        public static string ValidateStageCountChange(PositionQuestionnaireLockInfo lockInfo, int currentStageCount, int newStageCount)
        {
            if (lockInfo == null || !lockInfo.HasApplications)
            {
                return null;
            }

            var normalizedNew = Math.Max(1, newStageCount);
            if (normalizedNew < lockInfo.MinAllowedStageCount)
            {
                return string.Format(
                    "Questionnaire stage count cannot be reduced below {0} because candidates have already completed or answered questionnaire stages.",
                    lockInfo.MinAllowedStageCount);
            }

            return null;
        }

        public static string ValidateLegacyQuestionAssignments(
            PositionQuestionnaireLockInfo lockInfo,
            IList<PositionQuestion> existingAssignments,
            IList<PositionQuestionAssignmentInput> proposedAssignments)
        {
            if (lockInfo == null || !lockInfo.HasApplications)
            {
                return null;
            }

            if (lockInfo.LockedStageNumbers == null || !lockInfo.LockedStageNumbers.Contains(1))
            {
                return null;
            }

            var existing = (existingAssignments ?? new List<PositionQuestion>())
                .Where(pq => pq != null)
                .OrderBy(pq => pq.Order)
                .Select(pq => new LegacyAssignmentSnapshot
                {
                    QuestionId = pq.QuestionId,
                    Order = pq.Order,
                    Weight = pq.Weight
                })
                .ToList();

            var proposed = (proposedAssignments ?? new List<PositionQuestionAssignmentInput>())
                .Where(a => a != null && a.QuestionId > 0)
                .OrderBy(a => a.Order <= 0 ? int.MaxValue : a.Order)
                .ThenBy(a => a.QuestionId)
                .Select((a, index) => new LegacyAssignmentSnapshot
                {
                    QuestionId = a.QuestionId,
                    Order = a.Order > 0 ? a.Order : index + 1,
                    Weight = a.Weight
                })
                .ToList();

            if (!LegacyAssignmentsMatch(existing, proposed))
            {
                return lockInfo.BuildLockMessage()
                    ?? "You cannot edit questions because candidates have already submitted answers.";
            }

            return null;
        }

        public static string ValidateMultiStageQuestionSync(
            PositionQuestionnaireLockInfo lockInfo,
            IList<PositionQuestion> existingAssignments,
            IList<QuestionStageAssignment> proposedAssignments,
            IDictionary<int, decimal> proposedWeights,
            int currentStageCount,
            int proposedStageCount)
        {
            if (lockInfo == null || lockInfo.LockedStageNumbers == null || lockInfo.LockedStageNumbers.Count == 0)
            {
                return null;
            }

            var stageCountError = ValidateStageCountChange(lockInfo, currentStageCount, proposedStageCount);
            if (!string.IsNullOrEmpty(stageCountError))
            {
                return stageCountError;
            }

            var existing = existingAssignments ?? new List<PositionQuestion>();
            var proposed = proposedAssignments ?? new List<QuestionStageAssignment>();
            var weights = proposedWeights ?? new Dictionary<int, decimal>();

            foreach (var lockedStage in lockInfo.LockedStageNumbers.OrderBy(s => s))
            {
                var existingKeys = existing
                    .Where(pq => NormalizeStageNumber(pq.StageNumber) == lockedStage)
                    .Select(pq => pq.QuestionId)
                    .OrderBy(id => id)
                    .ToList();

                var proposedKeys = proposed
                    .Where(a => NormalizeStageNumber(a.StageNumber) == lockedStage)
                    .Select(a => a.QuestionId)
                    .OrderBy(id => id)
                    .ToList();

                if (!existingKeys.SequenceEqual(proposedKeys))
                {
                    return lockInfo.BuildLockMessage()
                        ?? "You cannot edit questions for a stage that already has candidate answers.";
                }

                foreach (var questionId in existingKeys)
                {
                    var existingWeight = existing
                        .Where(pq => pq.QuestionId == questionId)
                        .Select(pq => pq.Weight ?? 0m)
                        .FirstOrDefault();

                    decimal proposedWeight;
                    if (!weights.TryGetValue(questionId, out proposedWeight))
                    {
                        proposedWeight = 0m;
                    }

                    if (Math.Round(existingWeight, 2, MidpointRounding.AwayFromZero)
                        != Math.Round(proposedWeight, 2, MidpointRounding.AwayFromZero))
                    {
                        return lockInfo.BuildLockMessage()
                            ?? "You cannot edit question weights for a stage that already has candidate answers.";
                    }
                }
            }

            return null;
        }

        private static bool LegacyAssignmentsMatch(IList<LegacyAssignmentSnapshot> existing, IList<LegacyAssignmentSnapshot> proposed)
        {
            if (existing.Count != proposed.Count)
            {
                return false;
            }

            for (var i = 0; i < existing.Count; i++)
            {
                if (existing[i].QuestionId != proposed[i].QuestionId)
                {
                    return false;
                }

                if (existing[i].Order != proposed[i].Order)
                {
                    return false;
                }

                if (Math.Round(existing[i].Weight ?? 0m, 2, MidpointRounding.AwayFromZero)
                    != Math.Round(proposed[i].Weight ?? 0m, 2, MidpointRounding.AwayFromZero))
                {
                    return false;
                }
            }

            return true;
        }

        private static int NormalizeStageNumber(int stageNumber)
        {
            return stageNumber <= 0 ? 1 : stageNumber;
        }

        private sealed class LegacyAssignmentSnapshot
        {
            public int QuestionId { get; set; }
            public int Order { get; set; }
            public decimal? Weight { get; set; }
        }
    }

    public sealed class QuestionStageAssignment
    {
        public int QuestionId { get; set; }
        public int StageNumber { get; set; }
    }
}
