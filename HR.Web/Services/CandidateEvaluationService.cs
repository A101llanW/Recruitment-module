using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Services
{
    public interface ICandidateEvaluationService
    {
        CandidateScore EvaluateApplication(int applicationId, ApplicationReviewViewModel review, List<ApplicationAnswer> answers);
        decimal EvaluateIndividualAnswer(string positionTitle, string answerText);
    }

    public class CandidateScore
    {
        public CandidateScore()
        {
            CategoryScores = new Dictionary<string, decimal>();
        }

        public decimal Score { get; set; } // 0-100
        public string Reason { get; set; }
        public Dictionary<string, decimal> CategoryScores { get; set; }
    }

    public class CandidateEvaluationService : ICandidateEvaluationService
    {
        private readonly UnitOfWork _uow = new UnitOfWork();

        public CandidateScore EvaluateApplication(int applicationId, ApplicationReviewViewModel review, List<ApplicationAnswer> answers)
        {
            var score = new CandidateScore();
            var categoryScores = new Dictionary<string, decimal>();

            // Ensure we have a consolidated view of all text provided by the candidate
            string allAnswersText = string.Join(" ", (answers ?? new List<ApplicationAnswer>()).Select(a => a.AnswerText));

            // 1. Answer Completeness (20 points)
            decimal completenessScore = EvaluateCompleteness(review, answers);
            categoryScores["Completeness"] = completenessScore;

            // 2. Answer Quality & Relevance (30 points)
            decimal qualityScore = EvaluateQuality(review, answers, review.PositionTitle);
            categoryScores["Quality"] = qualityScore;

            // 3. Experience Level (25 points)
            decimal experienceScore = EvaluateExperience(review, allAnswersText);
            categoryScores["Experience"] = experienceScore;

            // 4. Motivation & Fit (15 points)
            decimal motivationScore = EvaluateMotivation(review, allAnswersText);
            categoryScores["Motivation"] = motivationScore;

            // 5. Professionalism (10 points)
            decimal professionalismScore = EvaluateProfessionalism(review, answers);
            categoryScores["Professionalism"] = professionalismScore;

            // Calculate total score
            var baseScore = completenessScore + qualityScore + experienceScore + motivationScore + professionalismScore;
            score.Score = Math.Round(baseScore, 2);

            // Generate reason
            score.Reason = GenerateScoreReason(score.Score, categoryScores);
            score.CategoryScores = categoryScores;

            // DEBUG: Log the calculated score
            System.Diagnostics.Debug.WriteLine("=== CANDIDATE SCORE CALCULATION ===");
            System.Diagnostics.Debug.WriteLine("Application ID: " + applicationId);
            System.Diagnostics.Debug.WriteLine("Base Score: " + baseScore);
            System.Diagnostics.Debug.WriteLine("Completeness: " + completenessScore);
            System.Diagnostics.Debug.WriteLine("Quality: " + qualityScore);
            System.Diagnostics.Debug.WriteLine("Experience: " + experienceScore);
            System.Diagnostics.Debug.WriteLine("Motivation: " + motivationScore);
            System.Diagnostics.Debug.WriteLine("Professionalism: " + professionalismScore);
            System.Diagnostics.Debug.WriteLine("FINAL SCORE: " + score.Score);
            System.Diagnostics.Debug.WriteLine("===============================");

            return score;
        }

        public decimal EvaluateIndividualAnswer(string positionTitle, string answerText)
        {
            if (string.IsNullOrWhiteSpace(answerText)) return 0;

            int rating;
            if (int.TryParse(answerText, out rating))
            {
                // Normalize a 1-5 rating to a 0-10 score (or simply return rating * 2) 
                return Math.Round(Math.Min(rating * 2m, 10m), 1);
            }

            decimal score = 0;
            
            // Length points
            if (answerText.Length > 20) score += 2;
            if (answerText.Length > 100) score += 3;
            
            score += CalculateVocabularyScore(answerText);
            score += GetRoleBonus(positionTitle, answerText);
            score += DetectSignals(answerText);

            return Math.Round(Math.Max(0, Math.Min(score, 10m)), 1);
        }

        private decimal EvaluateCompleteness(ApplicationReviewViewModel review, List<ApplicationAnswer> answers)
        {
            decimal score = 0;
            int totalFields = 0;
            int completedFields = 0;

            // Check standard fields
            // Only count them if they are actually used in the form (we check if ANY of them are filled)
            bool usingStandardFields = !string.IsNullOrWhiteSpace(review.WhyInterested) || 
                                     !string.IsNullOrWhiteSpace(review.YearsInField) ||
                                     !string.IsNullOrWhiteSpace(review.EducationLevel);

            if (usingStandardFields)
            {
                totalFields += 12;
                if (!string.IsNullOrWhiteSpace(review.WhyInterested)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.InterestLevel)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.YearsInField)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.YearsInRole)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.ExpectedSalary)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.EducationLevel)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.WorkAvailability)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.WorkMode)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.AvailabilityToStart)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.CommunicationSkills)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.ProblemSolvingSkills)) completedFields++;
                if (!string.IsNullOrWhiteSpace(review.TeamworkSkills)) completedFields++;
            }

            // Check dynamic answers
            if (answers != null && answers.Any())
            {
                totalFields += answers.Count;
                completedFields += answers.Count(a => !string.IsNullOrWhiteSpace(a.AnswerText));
            }

            // Check resume
            if (!string.IsNullOrWhiteSpace(review.ResumePath)) completedFields++;
            totalFields++;

            if (totalFields > 0)
            {
                score = (completedFields / (decimal)totalFields) * 20m;
            }

            return Math.Round(score, 2);
        }

        private decimal EvaluateQuality(ApplicationReviewViewModel review, List<ApplicationAnswer> answers, string positionTitle)
        {
            decimal score = 0;
            string combinedText = string.Join(" ", (answers ?? new List<ApplicationAnswer>()).Select(a => a.AnswerText ?? ""));

            // 1. Score based on interest reasons (if available)
            score += ScoreInterestReasons(review);

            // 2. Vocabulary Diversity & Richness (The Semantic Score)
            decimal diversityScore = CalculateVocabularyScore(combinedText);
            score += diversityScore;

            // 3. Role-Based Keyword Alignment
            decimal roleScore = GetRoleBonus(positionTitle, combinedText);
            score += roleScore;

            // 4. Signal Detection (Red vs Green Flags)
            decimal signalScore = DetectSignals(combinedText);
            score += signalScore;

            // 5. Evaluate dynamic answers quality (Legacy length-based + Numeric)
            score += ScoreDynamicAnswerRatings(answers);

            return Math.Round(Math.Min(score, 30m), 2);
        }

        private static decimal ScoreInterestReasons(ApplicationReviewViewModel review)
        {
            if (string.IsNullOrWhiteSpace(review.WhyInterested))
            {
                return 0;
            }

            var reasonCount = review.WhyInterested.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (reasonCount >= 4)
            {
                return 6;
            }

            return reasonCount >= 2 ? 3 : 0;
        }

        private static decimal ScoreDynamicAnswerRatings(List<ApplicationAnswer> answers)
        {
            if (answers == null || !answers.Any())
            {
                return 0;
            }

            decimal score = 0;
            foreach (var answer in answers)
            {
                if (string.IsNullOrWhiteSpace(answer.AnswerText))
                {
                    continue;
                }

                int rating;
                if (int.TryParse(answer.AnswerText, out rating))
                {
                    score += rating / 2m;
                }
            }

            return score;
        }

        private decimal CalculateVocabularyScore(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 50) return 0;

            var words = text.ToLower().Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(w => w.Length > 3).ToList();
            
            if (words.Count < 5) return 0;

            var uniqueWords = new HashSet<string>(words);
            double diversityRatio = (double)uniqueWords.Count / words.Count;

            // Higher score for rich vocabulary, lower for repetitive filler
            if (diversityRatio > 0.7) return 8;
            if (diversityRatio > 0.5) return 5;
            return 2;
        }

        private decimal GetRoleBonus(string role, string text)
        {
            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(text)) return 0;
            
            decimal bonus = 0;
            string lowerText = text.ToLower();
            string lowerRole = role.ToLower();

            var clusters = new Dictionary<string, string[]> {
                { "teacher", new[] { "curriculum", "pedagogy", "inclusive", "lesson", "student-centered", "classroom", "learning", "mentor", "instructional", "academic" } },
                { "developer", new[] { "stack", "frontend", "backend", "api", "database", "scaling", "agile", "sprint", "refactor", "deployment", "git" } },
                { "manager", new[] { "leadership", "budget", "strategy", "roadmap", "stakeholder", "performance", "kpi", "team", "operational", "efficiency" } }
            };

            foreach (var cluster in clusters)
            {
                if (lowerRole.Contains(cluster.Key))
                {
                    int matches = cluster.Value.Count(keyword => lowerText.Contains(keyword));
                    bonus += Math.Min(matches * 1.5m, 10m); // Up to 10 points for subject matter expertise
                    break;
                }
            }
            return bonus;
        }

        private decimal DetectSignals(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            return ScorePositiveSignals(text) + ScoreNegativeSignals(text);
        }

        private static decimal ScorePositiveSignals(string text)
        {
            decimal score = 0;

            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+%|\d+ students|\d+ years|achieved|delivered|improved"))
            {
                score += 4;
            }

            var lowerText = text.ToLower();
            if (lowerText.Contains("passion") || lowerText.Contains("dedicated") || lowerText.Contains("committed"))
            {
                score += 3;
            }

            return score;
        }

        private static decimal ScoreNegativeSignals(string text)
        {
            decimal score = 0;
            int capsCount = text.Count(c => char.IsUpper(c));
            if (capsCount > text.Length * 0.3)
            {
                score -= 5;
            }

            if (!text.Contains(".") && text.Length > 50)
            {
                score -= 3;
            }

            return score;
        }

        private decimal EvaluateExperience(ApplicationReviewViewModel review, string fallbackText = "")
        {
            decimal score = 0;

            // Try to get years from review or fallback text
            string yearsInFieldStr = review.YearsInField;
            if (string.IsNullOrWhiteSpace(yearsInFieldStr) && !string.IsNullOrWhiteSpace(fallbackText))
            {
                // Simple heuristic: look for "X years" in all answers
                var match = System.Text.RegularExpressions.Regex.Match(fallbackText, @"(\d+)\+?\s*years?");
                if (match.Success) yearsInFieldStr = match.Groups[1].Value;
            }

            // Years in field
            if (!string.IsNullOrWhiteSpace(yearsInFieldStr))
            {
                var yearsInField = ParseYears(yearsInFieldStr);
                if (yearsInField >= 5) score += 10;
                else if (yearsInField >= 3) score += 7;
                else if (yearsInField >= 1) score += 4;
                else score += 1;
            }

            // Years in role
            if (!string.IsNullOrWhiteSpace(review.YearsInRole))
            {
                var yearsInRole = ParseYears(review.YearsInRole);
                if (yearsInRole >= 3) score += 8;
                else if (yearsInRole >= 1) score += 5;
                else score += 2;
            }

            // Education level
            string eduText = (review.EducationLevel ?? "") + " " + fallbackText;
            if (!string.IsNullOrWhiteSpace(eduText))
            {
                var edu = eduText.ToLower();
                if (edu.Contains("master") || edu.Contains("phd") || edu.Contains("doctorate"))
                    score += 7;
                else if (edu.Contains("bachelor") || edu.Contains("degree") || edu.Contains("graduate"))
                    score += 5;
                else if (edu.Contains("diploma") || edu.Contains("certificate"))
                    score += 3;
                else if (edu.Contains("high school"))
                    score += 1;
            }

            // Cap at 25 points
            return Math.Round(Math.Min(score, 25m), 2);
        }

        private decimal EvaluateMotivation(ApplicationReviewViewModel review, string fallbackText = "")
        {
            decimal score = 0;

            // Interest level
            int interestLevel;
            if (!string.IsNullOrWhiteSpace(review.InterestLevel) && int.TryParse(review.InterestLevel, out interestLevel))
            {
                score += interestLevel;
            }
            else if (fallbackText.ToLower().Contains("passionate") || fallbackText.ToLower().Contains("excited"))
            {
                score += 3;
            }

            // Number of reasons
            if (!string.IsNullOrWhiteSpace(review.WhyInterested))
            {
                var reasons = review.WhyInterested.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var reasonCount = reasons.Length;
                if (reasonCount >= 4) score += 5;
                else if (reasonCount >= 3) score += 4;
                else if (reasonCount >= 2) score += 3;
                else if (reasonCount >= 1) score += 2;
            }

            // Availability
            string availability = (review.AvailabilityToStart ?? "") + " " + (review.WorkAvailability ?? "");
            if (!string.IsNullOrWhiteSpace(availability))
            {
                var avail = availability.ToLower();
                if (avail.Contains("immediate") || avail.Contains("now")) score += 4;
                else if (avail.Contains("2 weeks")) score += 3;
                else if (avail.Contains("month")) score += 2;
                else score += 1;
            }

            // Cap at 15 points
            return Math.Round(Math.Min(score, 15m), 2);
        }

        private decimal EvaluateProfessionalism(ApplicationReviewViewModel review, List<ApplicationAnswer> answers)
        {
            decimal score = 0;

            // Resume provided
            if (!string.IsNullOrWhiteSpace(review.ResumePath))
                score += 5;

            // Skill assessments
            if (!string.IsNullOrWhiteSpace(review.CommunicationSkills) || 
                !string.IsNullOrWhiteSpace(review.ProblemSolvingSkills) ||
                !string.IsNullOrWhiteSpace(review.TeamworkSkills))
            {
                decimal skillSum = 0;
                int count = 0;
                int val;
                if (int.TryParse(review.CommunicationSkills, out val)) { skillSum += val; count++; }
                if (int.TryParse(review.ProblemSolvingSkills, out val)) { skillSum += val; count++; }
                if (int.TryParse(review.TeamworkSkills, out val)) { skillSum += val; count++; }
                
                if (count > 0)
                {
                    decimal avg = skillSum / count;
                    if (avg >= 4) score += 3;
                    else if (avg >= 3) score += 2;
                    else score += 1;
                }
            }
            else if (answers != null && answers.Any(a => a.AnswerText != null && a.AnswerText.Length > 100))
            {
                // Bonus for detailed answers as a sign of professionalism
                score += 2;
            }

            // Comprehensive form completion
            if (answers != null && answers.All(a => !string.IsNullOrWhiteSpace(a.AnswerText)))
                score += 2;

            return Math.Round(Math.Min(score, 10m), 2);
        }

        private int ParseYears(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return 0;

            input = input.ToLower().Trim();
            
            var match = System.Text.RegularExpressions.Regex.Match(input, @"(\d+)");
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }

            if (input.Contains("five")) return 5;
            if (input.Contains("three")) return 3;
            if (input.Contains("one")) return 1;

            return 0;
        }

        private string GenerateScoreReason(decimal totalScore, Dictionary<string, decimal> categoryScores)
        {
            return string.Empty;
        }
    }
}

