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
            if (review == null)
            {
                return score;
            }

            var reviewData = review;
            var categoryScores = new Dictionary<string, decimal>();

            // Ensure we have a consolidated view of all text provided by the candidate
            string allAnswersText = string.Join(" ", (answers ?? new List<ApplicationAnswer>())
                .Where(a => a != null)
                .Select(a => a.AnswerText ?? string.Empty));

            // 1. Answer Completeness (20 points)
            decimal completenessScore = EvaluateCompleteness(reviewData, answers);
            categoryScores["Completeness"] = completenessScore;

            // 2. Answer Quality & Relevance (30 points)
            decimal qualityScore = EvaluateQuality(reviewData, answers, reviewData.PositionTitle);
            categoryScores["Quality"] = qualityScore;

            // 3. Experience Level (25 points)
            decimal experienceScore = EvaluateExperience(reviewData, allAnswersText);
            categoryScores["Experience"] = experienceScore;

            // 4. Motivation & Fit (15 points)
            decimal motivationScore = EvaluateMotivation(reviewData, allAnswersText);
            categoryScores["Motivation"] = motivationScore;

            // 5. Professionalism (10 points)
            decimal professionalismScore = EvaluateProfessionalism(reviewData, answers);
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
            if (review == null)
            {
                return 0;
            }

            var reviewData = review;
            int totalFields = 0;
            int completedFields = 0;

            CountStandardProfileFields(reviewData, ref totalFields, ref completedFields);
            CountDynamicAnswerFields(answers, ref totalFields, ref completedFields);

            if (!string.IsNullOrWhiteSpace(reviewData.ResumePath))
            {
                completedFields++;
            }

            totalFields++;

            if (totalFields <= 0)
            {
                return 0;
            }

            return Math.Round((completedFields / (decimal)totalFields) * 20m, 2);
        }

        private static void CountStandardProfileFields(ApplicationReviewViewModel review, ref int totalFields, ref int completedFields)
        {
            if (review == null || !UsesStandardProfileFields(review))
            {
                return;
            }

            totalFields += StandardProfileFieldAccessors.Length;
            completedFields += StandardProfileFieldAccessors.Count(accessor => !string.IsNullOrWhiteSpace(accessor(review)));
        }

        private static bool UsesStandardProfileFields(ApplicationReviewViewModel review)
        {
            return !string.IsNullOrWhiteSpace(review.WhyInterested) ||
                   !string.IsNullOrWhiteSpace(review.YearsInField) ||
                   !string.IsNullOrWhiteSpace(review.EducationLevel);
        }

        private static readonly Func<ApplicationReviewViewModel, string>[] StandardProfileFieldAccessors =
        {
            review => review.WhyInterested,
            review => review.InterestLevel,
            review => review.YearsInField,
            review => review.YearsInRole,
            review => review.ExpectedSalary,
            review => review.EducationLevel,
            review => review.WorkAvailability,
            review => review.WorkMode,
            review => review.AvailabilityToStart,
            review => review.CommunicationSkills,
            review => review.ProblemSolvingSkills,
            review => review.TeamworkSkills
        };

        private static void CountDynamicAnswerFields(List<ApplicationAnswer> answers, ref int totalFields, ref int completedFields)
        {
            if (answers == null || !answers.Any())
            {
                return;
            }

            totalFields += answers.Count;
            completedFields += answers.Count(a => a != null && !string.IsNullOrWhiteSpace(a.AnswerText));
        }

        private decimal EvaluateQuality(ApplicationReviewViewModel review, List<ApplicationAnswer> answers, string positionTitle)
        {
            if (review == null)
            {
                return 0;
            }

            var reviewData = review;
            decimal score = 0;
            string combinedText = string.Join(" ", (answers ?? new List<ApplicationAnswer>())
                .Where(a => a != null)
                .Select(a => a.AnswerText ?? string.Empty));

            // 1. Score based on interest reasons (if available)
            score += ScoreInterestReasons(reviewData);

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
            if (review == null || string.IsNullOrWhiteSpace(review.WhyInterested))
            {
                return 0;
            }

            var reviewData = review;
            var reasonCount = reviewData.WhyInterested.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
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
                if (answer == null || string.IsNullOrWhiteSpace(answer.AnswerText))
                {
                    continue;
                }

                var answerText = answer.AnswerText;
                int rating;
                if (int.TryParse(answerText, out rating))
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
            if (review == null)
            {
                return 0;
            }

            var reviewData = review;
            var safeFallbackText = fallbackText ?? string.Empty;
            var yearsInFieldStr = ResolveYearsInField(reviewData.YearsInField, safeFallbackText);
            var score = ScoreYearsInField(yearsInFieldStr) + ScoreYearsInRole(reviewData.YearsInRole) + ScoreEducation(reviewData.EducationLevel, safeFallbackText);
            return Math.Round(Math.Min(score, 25m), 2);
        }

        private static string ResolveYearsInField(string yearsInField, string fallbackText)
        {
            if (!string.IsNullOrWhiteSpace(yearsInField))
            {
                return yearsInField;
            }

            if (string.IsNullOrWhiteSpace(fallbackText))
            {
                return null;
            }

            var match = System.Text.RegularExpressions.Regex.Match(fallbackText, @"(\d+)\+?\s*years?");
            return match.Success ? match.Groups[1].Value : null;
        }

        private decimal ScoreYearsInField(string yearsInFieldStr)
        {
            if (string.IsNullOrWhiteSpace(yearsInFieldStr))
            {
                return 0;
            }

            var yearsInField = ParseYears(yearsInFieldStr);
            if (yearsInField >= 5) return 10;
            if (yearsInField >= 3) return 7;
            if (yearsInField >= 1) return 4;
            return 1;
        }

        private decimal ScoreYearsInRole(string yearsInRoleStr)
        {
            if (string.IsNullOrWhiteSpace(yearsInRoleStr))
            {
                return 0;
            }

            var yearsInRole = ParseYears(yearsInRoleStr);
            if (yearsInRole >= 3) return 8;
            if (yearsInRole >= 1) return 5;
            return 2;
        }

        private static decimal ScoreEducation(string educationLevel, string fallbackText)
        {
            var eduText = (educationLevel ?? string.Empty) + " " + (fallbackText ?? string.Empty);
            if (string.IsNullOrWhiteSpace(eduText.Trim()))
            {
                return 0;
            }

            return MatchEducationScore(eduText.ToLower());
        }

        private static decimal MatchEducationScore(string edu)
        {
            if (ContainsAny(edu, "master", "phd", "doctorate"))
            {
                return 7;
            }

            if (ContainsAny(edu, "bachelor", "degree", "graduate"))
            {
                return 5;
            }

            if (ContainsAny(edu, "diploma", "certificate"))
            {
                return 3;
            }

            return edu.Contains("high school") ? 1 : 0;
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            return terms.Any(text.Contains);
        }

        private decimal EvaluateMotivation(ApplicationReviewViewModel review, string fallbackText = "")
        {
            if (review == null)
            {
                return 0;
            }

            var reviewData = review;
            var safeFallbackText = fallbackText ?? string.Empty;
            var score = ScoreInterestLevel(reviewData.InterestLevel, safeFallbackText) +
                        ScoreWhyInterestedReasons(reviewData.WhyInterested) +
                        ScoreAvailability(reviewData.AvailabilityToStart, reviewData.WorkAvailability);
            return Math.Round(Math.Min(score, 15m), 2);
        }

        private static decimal ScoreInterestLevel(string interestLevel, string fallbackText)
        {
            int parsedInterest;
            if (!string.IsNullOrWhiteSpace(interestLevel) && int.TryParse(interestLevel, out parsedInterest))
            {
                return parsedInterest;
            }

            var lowerFallback = fallbackText != null ? fallbackText.ToLower() : string.Empty;
            return lowerFallback.Contains("passionate") || lowerFallback.Contains("excited") ? 3 : 0;
        }

        private static decimal ScoreWhyInterestedReasons(string whyInterested)
        {
            if (string.IsNullOrWhiteSpace(whyInterested))
            {
                return 0;
            }

            var reasonCount = whyInterested.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (reasonCount >= 4) return 5;
            if (reasonCount >= 3) return 4;
            if (reasonCount >= 2) return 3;
            if (reasonCount >= 1) return 2;
            return 0;
        }

        private static decimal ScoreAvailability(string availabilityToStart, string workAvailability)
        {
            var availability = (availabilityToStart ?? string.Empty) + " " + (workAvailability ?? string.Empty);
            if (string.IsNullOrWhiteSpace(availability))
            {
                return 0;
            }

            var avail = availability.ToLower();
            if (avail.Contains("immediate") || avail.Contains("now")) return 4;
            if (avail.Contains("2 weeks")) return 3;
            if (avail.Contains("month")) return 2;
            return 1;
        }

        private decimal EvaluateProfessionalism(ApplicationReviewViewModel review, List<ApplicationAnswer> answers)
        {
            if (review == null)
            {
                return 0;
            }

            var reviewData = review;
            var score = ScoreResumeProvided(reviewData.ResumePath) +
                        ScoreSkillAssessments(reviewData) +
                        ScoreDetailedAnswersBonus(reviewData, answers) +
                        ScoreCompleteDynamicAnswers(answers);
            return Math.Round(Math.Min(score, 10m), 2);
        }

        private static decimal ScoreResumeProvided(string resumePath)
        {
            return string.IsNullOrWhiteSpace(resumePath) ? 0 : 5;
        }

        private static decimal ScoreSkillAssessments(ApplicationReviewViewModel review)
        {
            if (review == null)
            {
                return 0;
            }

            var ratings = ParseSkillRatings(review);
            if (ratings.Count == 0)
            {
                return 0;
            }

            return ScoreAverageSkillRating(ratings.Average());
        }

        private static List<decimal> ParseSkillRatings(ApplicationReviewViewModel review)
        {
            var ratings = new List<decimal>();
            foreach (var fieldValue in new[] { review.CommunicationSkills, review.ProblemSolvingSkills, review.TeamworkSkills })
            {
                int parsedValue;
                if (int.TryParse(fieldValue, out parsedValue))
                {
                    ratings.Add(parsedValue);
                }
            }

            return ratings;
        }

        private static decimal ScoreAverageSkillRating(decimal average)
        {
            if (average >= 4)
            {
                return 3;
            }

            return average >= 3 ? 2 : 1;
        }

        private static bool HasSkillAssessments(ApplicationReviewViewModel review)
        {
            return review != null && (
                !string.IsNullOrWhiteSpace(review.CommunicationSkills) ||
                !string.IsNullOrWhiteSpace(review.ProblemSolvingSkills) ||
                !string.IsNullOrWhiteSpace(review.TeamworkSkills));
        }

        private static decimal ScoreDetailedAnswersBonus(ApplicationReviewViewModel review, List<ApplicationAnswer> answers)
        {
            if (HasSkillAssessments(review))
            {
                return 0;
            }

            return HasDetailedDynamicAnswers(answers) ? 2 : 0;
        }

        private static bool HasDetailedDynamicAnswers(List<ApplicationAnswer> answers)
        {
            return answers != null &&
                   answers.Any(a => a != null && a.AnswerText != null && a.AnswerText.Length > 100);
        }

        private static decimal ScoreCompleteDynamicAnswers(List<ApplicationAnswer> answers)
        {
            return answers != null && answers.All(a => a != null && !string.IsNullOrWhiteSpace(a.AnswerText)) ? 2 : 0;
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

