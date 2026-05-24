using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        private readonly ScoringService _scoringService = new ScoringService();

        /// <summary>
        /// Enhanced candidate rankings with detailed scoring
        /// </summary>
        public ActionResult EnhancedCandidateRankings(int? positionId)
        {
            var positions = _uow.Positions.GetAll(p => p.Department).ToList();
            var rankings = new Dictionary<Position, List<CandidateRanking>>();

            if (positionId.HasValue)
            {
                var position = positions.FirstOrDefault(p => p != null && p.Id == positionId.Value);
                if (position != null)
                {
                    rankings[position] = _scoringService.RankCandidatesForPosition(positionId.Value)
                        ?? new List<CandidateRanking>();
                }
            }
            else
            {
                // Get rankings for all positions
                foreach (var position in positions.Where(p => p != null && p.IsOpen))
                {
                    rankings[position] = _scoringService.RankCandidatesForPosition(position.Id)
                        ?? new List<CandidateRanking>();
                }
            }

            var viewModel = new EnhancedCandidateRankingsViewModel
            {
                Positions = positions,
                RankingsByPosition = rankings,
                SelectedPositionId = positionId
            };

            return View(viewModel);
        }

        /// <summary>
        /// Get detailed score breakdown for an application
        /// </summary>
        public ActionResult ApplicationScoreDetails(int applicationId)
        {
            var application = _uow.Applications.Get(
                applicationId,
                a => a.Applicant,
                a => a.Position);
            if (application == null || application.Position == null)
            {
                return HttpNotFound();
            }

            var scopedApplication = application;
            var breakdown = _scoringService.GetScoreBreakdown(applicationId) ?? new List<QuestionScoreBreakdown>();
            var maxScore = _scoringService.GetMaxScoreForPosition(scopedApplication.PositionId);

            var viewModel = new ApplicationScoreDetailsViewModel
            {
                Application = scopedApplication,
                ScoreBreakdown = breakdown,
                TotalScore = breakdown.Sum(b => b.Score),
                MaxScore = maxScore,
                Percentage = maxScore > 0 ? (breakdown.Sum(b => b.Score) / maxScore) * 100 : 0
            };

            return View(viewModel);
        }

        /// <summary>
        /// Question performance analysis
        /// </summary>
        public async Task<ActionResult> QuestionAnalysis(int questionId)
        {
            var question = _uow.Questions.Get(questionId);
            if (question == null) return HttpNotFound();

            var analysis = await _scoringService.AnalyzeQuestionPerformance(questionId);

            var viewModel = new QuestionAnalysisViewModel
            {
                Question = question,
                PerformanceAnalysis = analysis
            };

            return View(viewModel);
        }


        /// <summary>
        /// Get scoring statistics for dashboard
        /// </summary>
        public ActionResult ScoringStatistics()
        {
            var positions = _uow.Positions.GetAll(p => p.Department).Where(p => p.IsOpen).ToList();
            var statistics = new List<PositionScoringStatistics>();

            foreach (var position in positions)
            {
                if (position == null)
                {
                    continue;
                }

                var scopedPosition = position;
                var rankings = _scoringService.RankCandidatesForPosition(scopedPosition.Id);
                var applications = rankings != null ? rankings.Count : 0;
                var averageScore = applications > 0 ? rankings.Average(r => r.Percentage) : 0;
                var maxScore = _scoringService.GetMaxScoreForPosition(scopedPosition.Id);

                statistics.Add(new PositionScoringStatistics
                {
                    PositionId = scopedPosition.Id,
                    PositionTitle = scopedPosition.Title,
                    DepartmentName = scopedPosition.Department != null ? scopedPosition.Department.Name : "Unknown",
                    ApplicationCount = applications,
                    AverageScore = averageScore,
                    MaxScore = maxScore,
                    TopCandidateScore = applications > 0 ? rankings.Max(r => r.Percentage) : 0
                });
            }

            return View(statistics);
        }

        /// <summary>
        /// Recalculate all scores for a position
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,HR")]
        [ValidateAntiForgeryToken]
        public ActionResult RecalculateScores(int positionId)
        {
            try
            {
                var applications = _uow.Applications.GetAll()
                    .Where(a => a.PositionId == positionId)
                    .ToList();

                var updatedCount = 0;
                foreach (var application in applications)
                {
                    var newScore = _scoringService.CalculateApplicationScore(application);
                    if (application.Score != newScore)
                    {
                        application.Score = newScore;
                        application.ScoreReason = "Questionnaire score recalculated.";
                        _uow.Applications.Update(application);
                        updatedCount++;
                    }
                }

                _uow.Complete();
                TempData["Message"] = string.Format("Scores recalculated for {0} applications.", updatedCount);
                return Json(new { success = true, updatedCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Export candidate rankings
        /// </summary>
        public ActionResult ExportRankings(int positionId)
        {
            var position = _uow.Positions.Get(positionId);
            if (position == null) return HttpNotFound();

            var rankings = _scoringService.RankCandidatesForPosition(positionId) ?? new List<CandidateRanking>();

            var csv = "Rank,Candidate Name,Email,Score,Max Score,Percentage,Applied Date,Status\n";
            
            for (int i = 0; i < rankings.Count; i++)
            {
                var rank = rankings[i];
                if (rank == null)
                {
                    continue;
                }

                csv += string.Format("{0},{1},{2},{3},{4},{5:F1}%,{6:yyyy-MM-dd},{7}\n",
                    i + 1,
                    rank.CandidateName ?? string.Empty,
                    rank.CandidateEmail ?? string.Empty,
                    rank.TotalScore,
                    rank.MaxScore,
                    rank.Percentage,
                    rank.AppliedDate,
                    rank.Status ?? string.Empty);
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", string.Format("rankings_{0}_{1:yyyyMMdd}.csv", position.Title, DateTime.Now));
        }

        /// <summary>
        /// API endpoint for real-time score updates
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateApplicationScore(int applicationId)
        {
            try
            {
                var application = _uow.Applications.Get(applicationId);
                if (application == null)
                {
                    return HttpNotFound();
                }

                var scopedApplication = application;
                var newScore = _scoringService.CalculateApplicationScore(scopedApplication);
                scopedApplication.Score = newScore;
                _uow.Applications.Update(scopedApplication);
                _uow.Complete();

                var breakdown = _scoringService.GetScoreBreakdown(applicationId) ?? new List<QuestionScoreBreakdown>();
                var maxScore = _scoringService.GetMaxScoreForPosition(scopedApplication.PositionId);

                return Json(new
                {
                    success = true,
                    score = newScore,
                    maxScore = maxScore,
                    percentage = maxScore > 0 ? (newScore / maxScore) * 100 : 0,
                    breakdown = breakdown.Select(b => new
                    {
                        questionId = b.QuestionId,
                        questionText = b.QuestionText,
                        score = b.Score,
                        maxScore = b.MaxScore,
                        percentage = b.Percentage
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Bulk score recalculation
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,HR")]
        [ValidateAntiForgeryToken]
        public ActionResult BulkRecalculateScores()
        {
            try
            {
                var allApplications = _uow.Applications.GetAll().ToList();
                var updatedCount = 0;

                foreach (var application in allApplications)
                {
                    var newScore = _scoringService.CalculateApplicationScore(application);
                    if (application.Score != newScore)
                    {
                        application.Score = newScore;
                        _uow.Applications.Update(application);
                        updatedCount++;
                    }
                }

                _uow.Complete();
                TempData["Message"] = string.Format("Bulk score recalculation completed. {0} applications updated.", updatedCount);
                return RedirectToAction("ScoringStatistics");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error during bulk recalculation: " + ex.Message;
                return RedirectToAction("ScoringStatistics");
            }
        }
    }

    // View Models
    public class EnhancedCandidateRankingsViewModel
    {
        public List<Position> Positions { get; set; }
        public Dictionary<Position, List<CandidateRanking>> RankingsByPosition { get; set; }
        public int? SelectedPositionId { get; set; }
    }

    public class ApplicationScoreDetailsViewModel
    {
        public Application Application { get; set; }
        public List<QuestionScoreBreakdown> ScoreBreakdown { get; set; }
        public decimal TotalScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
        public int Rank { get; set; }
    }

    public class QuestionAnalysisViewModel
    {
        public Question Question { get; set; }
        public QuestionPerformanceAnalysis PerformanceAnalysis { get; set; }
    }

    public class PositionScoringStatistics
    {
        public int PositionId { get; set; }
        public string PositionTitle { get; set; }
        public string DepartmentName { get; set; }
        public int ApplicationCount { get; set; }
        public decimal AverageScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal TopCandidateScore { get; set; }
    }
}
