using System.Collections.Generic;
using System.Linq;
using HR.Web.Models;

namespace HR.Web.Services
{
    public sealed class ApplicationRankResult
    {
        public int Rank { get; set; }
        public int TotalApplicants { get; set; }
        public decimal? Score { get; set; }
        public bool HasScore { get; set; }
    }

    /// <summary>
    /// Computes competition rank for an application within its position cohort.
    /// Higher score ranks better; ties share the same rank; unscored applications rank last.
    /// </summary>
    public static class ApplicationRankHelper
    {
        public static ApplicationRankResult ComputeRank(IEnumerable<Application> positionApplications, int applicationId)
        {
            var apps = positionApplications?.ToList() ?? new List<Application>();
            if (!apps.Any())
            {
                return new ApplicationRankResult
                {
                    Rank = 1,
                    TotalApplicants = 0,
                    HasScore = false
                };
            }

            var ordered = apps
                .OrderByDescending(a => a.Score.HasValue)
                .ThenByDescending(a => a.Score ?? 0m)
                .ThenBy(a => a.AppliedOn)
                .ThenBy(a => a.Id)
                .ToList();

            var rankByApplicationId = new Dictionary<int, int>();
            var currentRank = 1;
            decimal? previousScore = null;
            var previousHadScore = false;

            for (var index = 0; index < ordered.Count; index++)
            {
                var entry = ordered[index];
                var hasScore = entry.Score.HasValue;
                var score = entry.Score ?? 0m;

                if (index > 0 && (hasScore != previousHadScore || (hasScore && score != previousScore)))
                {
                    currentRank = index + 1;
                }

                rankByApplicationId[entry.Id] = currentRank;
                previousScore = hasScore ? score : (decimal?)null;
                previousHadScore = hasScore;
            }

            var target = apps.FirstOrDefault(a => a.Id == applicationId);
            return new ApplicationRankResult
            {
                Rank = rankByApplicationId.ContainsKey(applicationId) ? rankByApplicationId[applicationId] : ordered.Count,
                TotalApplicants = apps.Count,
                Score = target != null ? target.Score : null,
                HasScore = target != null && target.Score.HasValue
            };
        }
    }
}
