using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace HR.Web.Services
{
    public class ScoringService
    {
        private readonly UnitOfWork _uow;
        private readonly MCPService _mcpService;
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by",
            "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does",
            "did", "will", "would", "could", "should", "may", "might", "can", "what", "how", "when",
            "where", "why", "who", "whom", "which", "this", "that", "these", "those", "your", "you",
            "i", "me", "my", "we", "our", "us", "they", "them", "their", "it", "its", "as", "from",
            "into", "about", "over", "under", "up", "down", "out", "so", "if", "then", "than", "just",
            "also", "very", "really", "more", "most", "some", "any", "each", "few", "many", "much",
            "such", "only", "not", "no", "yes", "because", "since", "while", "during"
        };

        public ScoringService()
        {
            _uow = new UnitOfWork();
            _mcpService = new MCPService();
        }

        /// <summary>
        /// Calculate total score for an application based on questionnaire responses
        /// Returns percentage out of 100
        /// </summary>
        public decimal CalculateApplicationScore(int applicationId)
        {
            var application = _uow.Applications.Get(applicationId);
            if (application == null) return 0;

            return CalculateApplicationScore(application);
        }

        /// <summary>
        /// Calculate total score for an application as percentage out of 100
        /// </summary>
        public decimal CalculateApplicationScore(Application application)
        {
            var weightedScore = 0m;

            // Get position questions and their order
            var positionQuestions = _uow.Context.Set<PositionQuestion>()
                .Where(pq => pq.PositionId == application.PositionId)
                .Include(pq => pq.Question)
                .OrderBy(pq => pq.Order)
                .ToList();

            if (!positionQuestions.Any())
            {
                return 0;
            }

            var effectiveWeights = GetEffectiveQuestionWeights(positionQuestions);

            // Get application answers
            var answers = _uow.Context.Set<ApplicationAnswer>()
                .Where(aa => aa.ApplicationId == application.Id)
                .ToList();

            foreach (var positionQuestion in positionQuestions)
            {
                decimal questionWeight;
                if (!effectiveWeights.TryGetValue(positionQuestion.Id, out questionWeight))
                {
                    continue;
                }

                var rawMaxScore = GetMaxScoreForQuestion(positionQuestion.Question, application.PositionId);
                if (rawMaxScore <= 0)
                {
                    continue;
                }

                var answer = answers.FirstOrDefault(a => a.QuestionId == positionQuestion.QuestionId);
                if (answer != null)
                {
                    var rawQuestionScore = CalculateQuestionScore(positionQuestion.Question, answer.AnswerText, application.PositionId);
                    var normalizedScore = Clamp01(rawQuestionScore / rawMaxScore);
                    weightedScore += normalizedScore * questionWeight;
                }
            }

            // Calculate maximum possible score for this position
            var maxPossibleScore = GetMaxPossibleScoreForPosition(positionQuestions);
            
            // Convert to percentage out of 100
            var percentage = maxPossibleScore > 0 ? (weightedScore / maxPossibleScore) * 100 : 0;
            
            return Math.Max(0, Math.Min(100, percentage)); // Cap at 100%
        }

        /// <summary>
        /// Get maximum possible weighted score for a position
        /// </summary>
        private decimal GetMaxPossibleScoreForPosition(List<PositionQuestion> positionQuestions)
        {
            if (positionQuestions == null || !positionQuestions.Any())
            {
                return 0;
            }

            var effectiveWeights = GetEffectiveQuestionWeights(positionQuestions);
            return effectiveWeights.Values.Sum();
        }

        private Dictionary<int, decimal> GetEffectiveQuestionWeights(IList<PositionQuestion> positionQuestions)
        {
            var weights = new Dictionary<int, decimal>();
            if (positionQuestions == null || !positionQuestions.Any())
            {
                return weights;
            }

            if (positionQuestions.Count == 1)
            {
                weights[positionQuestions[0].Id] = 100m;
                return weights;
            }

            var rawWeights = positionQuestions
                .Select(pq => new
                {
                    pq.Id,
                    RawWeight = pq.Weight,
                    PositiveWeight = Math.Max(0m, pq.Weight ?? 0m)
                })
                .ToList();

            var configured = rawWeights.Where(w => w.RawWeight.HasValue && w.RawWeight.Value > 0m).ToList();
            var configuredTotal = configured.Sum(w => w.PositiveWeight);

            if (configuredTotal <= 0m)
            {
                var equalWeight = Math.Round(100m / rawWeights.Count, 2, MidpointRounding.AwayFromZero);
                foreach (var rawWeight in rawWeights)
                {
                    weights[rawWeight.Id] = equalWeight;
                }
            }
            else if (configuredTotal < 100m && configured.Count < rawWeights.Count)
            {
                foreach (var configuredWeight in configured)
                {
                    weights[configuredWeight.Id] = Math.Round(configuredWeight.PositiveWeight, 2, MidpointRounding.AwayFromZero);
                }

                var unconfiguredCount = rawWeights.Count - configured.Count;
                var remainder = 100m - configuredTotal;
                var unconfiguredWeight = Math.Round(remainder / unconfiguredCount, 2, MidpointRounding.AwayFromZero);
                foreach (var rawWeight in rawWeights.Where(w => !configured.Any(c => c.Id == w.Id)))
                {
                    weights[rawWeight.Id] = unconfiguredWeight;
                }
            }
            else
            {
                var totalRawWeight = rawWeights.Sum(w => w.PositiveWeight);
                foreach (var rawWeight in rawWeights)
                {
                    var scaledWeight = totalRawWeight > 0m ? (rawWeight.PositiveWeight / totalRawWeight) * 100m : 0m;
                    weights[rawWeight.Id] = Math.Round(scaledWeight, 2, MidpointRounding.AwayFromZero);
                }
            }

            var diff = 100m - weights.Values.Sum();
            var lastId = rawWeights.Last().Id;
            weights[lastId] += diff;

            return weights;
        }

        private static decimal Clamp01(decimal value)
        {
            if (value < 0m) return 0m;
            if (value > 1m) return 1m;
            return value;
        }

        /// <summary>
        /// Calculate score for a single question using MCPService for content-based evaluation
        /// </summary>
        public decimal CalculateQuestionScore(Question question, string answerText, int positionId)
        {
            if (string.IsNullOrEmpty(answerText)) return 0;
            // Use deterministic scoring to avoid async deadlocks during submission.
            return CalculateQuestionScoreFallback(question, answerText, positionId);
        }

        /// <summary>
        /// Fallback scoring method using traditional logic
        /// </summary>
        public decimal CalculateQuestionScoreFallback(Question question, string answerText, int positionId)
        {
            switch (question.Type.ToLower())
            {
                case "choice":
                    return CalculateChoiceScore(question, answerText, positionId);
                case "rating":
                    return CalculateRatingScore(question, answerText);
                case "number":
                    return CalculateNumberScore(question, answerText);
                case "text":
                    return CalculateTextScore(question, answerText, positionId);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Calculate score for choice questions (existing logic - now fallback)
        /// </summary>
        private decimal CalculateChoiceScoreFallback(Question question, string answerText, int positionId)
        {
            // Get question options
            var options = _uow.Context.Set<HR.Web.Models.QuestionOption>()
                .Where(qo => qo.QuestionId == question.Id)
                .ToList();

            // Check for position-specific overrides
            var positionQuestion = _uow.Context.Set<PositionQuestion>()
                .FirstOrDefault(pq => pq.PositionId == positionId && pq.QuestionId == question.Id);

            if (positionQuestion != null)
            {
                var positionOptions = _uow.Context.Set<PositionQuestionOption>()
                    .Where(pqo => pqo.PositionQuestionId == positionQuestion.Id)
                    .Include(pqo => pqo.QuestionOption)
                    .ToList();

                // Use position-specific points if available
                var matchedOption = positionOptions.FirstOrDefault(pqo => 
                    pqo.QuestionOption.Text.Equals(answerText, StringComparison.OrdinalIgnoreCase));

                if (matchedOption != null && matchedOption.Points.HasValue)
                {
                    return matchedOption.Points.Value;
                }
            }

            // Fallback to default points
            var defaultOption = options.FirstOrDefault(o => 
                o.Text.Equals(answerText, StringComparison.OrdinalIgnoreCase));

            return defaultOption != null ? defaultOption.Points : 0;
        }

        /// <summary>
        /// Calculate score for rating questions with AI enhancement
        /// </summary>
        private decimal CalculateRatingScore(Question question, string answerText)
        {
            int rating;
            if (int.TryParse(answerText, out rating))
            {
                // Convert rating to points (1-5 scale -> 0-10 points)
                return Math.Max(0, Math.Min(10, rating * 2));
            }

            return 0;
        }

        /// <summary>
        /// Calculate score for number questions with AI enhancement
        /// </summary>
        private decimal CalculateNumberScore(Question question, string answerText)
        {
            decimal number;
            if (decimal.TryParse(answerText, out number))
            {
                // For years of experience: 0-10 points based on experience level
                if (question.Text.ToLower().Contains("year") && question.Text.ToLower().Contains("experience"))
                {
                    return Math.Min(10, Math.Max(0, number * 2));
                }

                // For other numeric questions, use a simple scaling
                return Math.Min(10, Math.Max(0, number));
            }

            return 0;
        }

        /// <summary>
        /// Extract context from question text for AI evaluation
        /// </summary>
        private string ExtractQuestionContext(string questionText)
        {
            var lowerText = questionText.ToLower();
            
            if (lowerText.Contains("year") && lowerText.Contains("experience"))
                return "years of experience";
            if (lowerText.Contains("salary") || lowerText.Contains("compensation"))
                return "salary expectation";
            if (lowerText.Contains("team") || lowerText.Contains("manage"))
                return "team size";
            if (lowerText.Contains("hour") || lowerText.Contains("week"))
                return "time availability";
            
            return "general numeric response";
        }

        /// <summary>
        /// Calculate score for text questions using enhanced basic scoring
        /// </summary>
        private decimal CalculateTextScore(Question question, string answerText, int positionId)
        {
            // Skip fake AI evaluation and go directly to enhanced basic scoring
            return CalculateBasicTextScore(question, answerText, positionId);
        }

        /// <summary>
        /// Enhanced basic text scoring with stronger keyword extraction and answer strength detection
        /// </summary>
        private decimal CalculateBasicTextScore(Question question, string answerText, int positionId)
        {
            if (string.IsNullOrEmpty(answerText)) return 0;

            var score = 0m;
            var lowerText = answerText.ToLower();
            
            // Get position context for repetition checking and relevance
            var position = _uow.Context.Set<Position>().Find(positionId);
            var jobContext = position != null ? string.Format("{0} {1}", position.Description, position.Responsibilities).ToLower() : "";

            // 1. Enhanced length score
            score += CalculateLengthScore(answerText);

            // 2. Advanced keyword extraction with word boundaries
            score += ExtractAndScoreKeywords(question, answerText, lowerText, jobContext);

            // 3. Experience Intensity and Answer Strength
            score += AnalyzeAnswerStrength(answerText, lowerText);

            // 4. Professional communication
            score += AnalyzeProfessionalCommunication(lowerText);

            // 5. Contextual relevance
            score += AnalyzeContextualRelevance(question, answerText, lowerText);

            // 6. Semantic intensity (meaningful specificity and strength)
            score += CalculateSemanticIntensityScore(question, answerText, lowerText, jobContext);

            // 7. Technical indicators
            score += AnalyzeTechnicalIndicators(lowerText);

            // 8. Structure and coherence
            score += AnalyzeStructureAndCoherence(answerText, lowerText);

            // 9. Quality penalties (including Repetition Penalty)
            score = ApplyQualityAdjustments(score, answerText, lowerText, jobContext);

            var finalScore = Math.Max(0, score); // Removed cap - unlimited points per question
            System.Diagnostics.Debug.WriteLine(string.Format("Final enhanced score: {0}", finalScore));
            System.Diagnostics.Debug.WriteLine("=== END ENHANCED TEXT SCORING DEBUG ===");

            return finalScore;
        }

        /// <summary>
        /// Calculate length score with context awareness
        /// </summary>
        private decimal CalculateLengthScore(string answerText)
        {
            var score = 0m;
            
            // More nuanced length scoring
            if (answerText.Length < 10) score += 0.3m; // Very short - minimal credit
            else if (answerText.Length < 25) score += 0.8m;
            else if (answerText.Length < 50) score += 1.8m;
            else if (answerText.Length < 100) score += 3.2m;
            else if (answerText.Length < 200) score += 4.8m;
            else if (answerText.Length < 300) score += 6.2m;
            else if (answerText.Length < 500) score += 7.5m;
            else if (answerText.Length < 800) score += 8.5m;
            else score += 9m; // Diminishing returns for very long answers

            System.Diagnostics.Debug.WriteLine(string.Format("Enhanced length score: {0}", score));
            return score;
        }

        /// <summary>
        /// Advanced keyword extraction with semantic analysis
        /// </summary>
        private decimal ExtractAndScoreKeywords(Question question, string answerText, string lowerText, string jobContext)
        {
            var score = 0m;
            
            // Enhanced word tokenization
            var words = TokenizeText(lowerText);
            var uniqueWords = words.Distinct().ToList();
            
            // Vocabulary diversity scoring
            if (uniqueWords.Count >= 60) score += 2.5m;
            else if (uniqueWords.Count >= 40) score += 2m;
            else if (uniqueWords.Count >= 25) score += 1.5m;
            else if (uniqueWords.Count >= 15) score += 1m;
            else if (uniqueWords.Count >= 8) score += 0.5m;

            // Industry-specific keyword detection
            var industryKeywords = GetIndustryKeywords(question.Text, jobContext);
            var industryMatches = words.Count(w => industryKeywords.Contains(w));
            score += Math.Min(2m, industryMatches * 0.4m);

            // Action verb detection (strong indicators of experience)
            var actionVerbs = GetStrongActionVerbs();
            var actionMatches = words.Count(w => actionVerbs.Contains(w));
            score += Math.Min(1.5m, actionMatches * 0.3m);

            // Skill and technology detection
            var techKeywords = GetSkillKeywords(question.Text, jobContext);
            var normalizedTechKeywords = new HashSet<string>(techKeywords.Select(NormalizeToken).Where(k => !string.IsNullOrWhiteSpace(k)));
            var techMatches = uniqueWords.Count(w => normalizedTechKeywords.Contains(w));
            score += Math.Min(2m, techMatches * 0.5m);

            // Reward specificity for skills tied to the question context
            score += CalculateSpecificityBonus(question, words, jobContext);

            System.Diagnostics.Debug.WriteLine(string.Format("Keyword extraction score: {0} (Industry: {1}, Actions: {2}, Tech/Skill: {3})", score, industryMatches, actionMatches, techMatches));
            return score;
        }

        /// <summary>
        /// Analyze answer strength and specificity
        /// </summary>
        private decimal AnalyzeAnswerStrength(string answerText, string lowerText)
        {
            var score = 0m;

            // Experience Intensity: Look for years of experience and management scale
            var intensityPatterns = new[] {
                @"\b(managed|led|supervised)\s+\d+\s+people\b",
                @"\b\d+\s+years?\s+(of\s+)?experience\b",
                @"\bteam\s+lead(er)?\b",
                @"\bover\s+\d+\s+(projects|clients)\b"
            };
            var intensityCount = intensityPatterns.Count(p => Regex.IsMatch(lowerText, p, RegexOptions.IgnoreCase));
            score += Math.Min(3m, intensityCount * 1.0m);

            // Quantifiable evidence detection
            var quantifiablePatterns = new[] {
                @"\d+\s*(percent|%)\s+(increase|decrease|growth|reduction)",
                @"\$\s*\d+[kmb]?\s*(budget|revenue|salary|cost|savings)",
                @"\d+\s+(projects|initiatives|campaigns|products)"
            };

            var quantifiableCount = quantifiablePatterns.Count(pattern => 
                System.Text.RegularExpressions.Regex.IsMatch(lowerText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            score += Math.Min(2m, quantifiableCount * 0.7m);

            // Specific example indicators
            var examplePatterns = new[] {
                @"\b(for\s+example|for\s+instance|such\s+as|specifically|including)\b",
                @"\b(demonstrated|proven|implemented|executed|delivered)\b",
                @"\b(i\s+(have|was|am|did)\s+[\w\s]{5,30})\b",
                @"\b(in\s+my\s+role|as\s+a\s+[\w\s]{3,20})\b",
                @"\b(responsible\s+for|tasked\s+with|handled)\b"
            };

            var exampleCount = examplePatterns.Count(pattern => 
                System.Text.RegularExpressions.Regex.IsMatch(lowerText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            score += Math.Min(2m, exampleCount * 0.5m);

            // Result and outcome indicators
            var resultPatterns = new[] {
                @"\b(resulted\s+in|led\s+to|achieved|accomplished|succeeded)\b",
                @"\b(improved|increased|decreased|reduced|optimized|enhanced)\b",
                @"\b(saved|generated|created|developed|built)\b",
                @"\b(on\s+time|within\s+budget|met\s+deadline)\b"
            };

            var resultCount = resultPatterns.Count(pattern => 
                System.Text.RegularExpressions.Regex.IsMatch(lowerText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            score += Math.Min(1.5m, resultCount * 0.4m);

            return score;
        }

        /// <summary>
        /// Analyze professional communication patterns
        /// </summary>
        private decimal AnalyzeProfessionalCommunication(string lowerText)
        {
            var score = 0m;

            // Professional language patterns
            var professionalPatterns = new[] {
                @"\b(collaborated|partnered|coordinated|liaised)\b",
                @"\b(strategic|initiative|methodology|framework)\b",
                @"\b(stakeholder|client|customer|user)\b",
                @"\b(process|procedure|workflow|methodology)\b",
                @"\b(analysis|assessment|evaluation|review)\b"
            };

            var professionalCount = professionalPatterns.Count(pattern => 
                System.Text.RegularExpressions.Regex.IsMatch(lowerText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            score += Math.Min(1.5m, professionalCount * 0.3m);

            // Leadership and responsibility indicators
            var leadershipPatterns = new[] {
                @"\b(led|managed|supervised|mentored|trained)\b",
                @"\b(responsible\s+for|accountable\s+for|owned)\b",
                @"\b(my\s+team|our\s+team|team\s+lead)\b",
                @"\b(decision|strategy|vision|direction)\b"
            };

            var leadershipCount = leadershipPatterns.Count(pattern => 
                System.Text.RegularExpressions.Regex.IsMatch(lowerText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            score += Math.Min(1.5m, leadershipCount * 0.4m);

            System.Diagnostics.Debug.WriteLine(string.Format("Professional communication score: {0} (Professional: {1}, Leadership: {2})", score, professionalCount, leadershipCount));
            return score;
        }

        /// <summary>
        /// Analyze contextual relevance with semantic matching
        /// </summary>
        private decimal AnalyzeContextualRelevance(Question question, string answerText, string lowerText)
        {
            var score = 0m;
            
            // Enhanced question keyword extraction
            var questionKeywords = ExtractQuestionKeywords(question.Text);
            var answerWords = TokenizeText(lowerText);
            
            // Direct keyword matching
            var directMatches = answerWords.Count(w => questionKeywords.Contains(w));
            score += Math.Min(2m, directMatches * 0.3m);
            
            // Semantic similarity (partial matches and related terms)
            var semanticMatches = CalculateSemanticMatches(answerWords, questionKeywords);
            score += Math.Min(1.5m, semanticMatches * 0.2m);
            
            // Question type specific relevance
            score += CalculateQuestionTypeRelevance(question, answerText, lowerText);

            System.Diagnostics.Debug.WriteLine(string.Format("Contextual relevance score: {0} (Direct: {1}, Semantic: {2})", score, directMatches, semanticMatches));
            return score;
        }

        /// <summary>
        /// Evaluate semantic intensity and specificity for text answers
        /// </summary>
        private decimal CalculateSemanticIntensityScore(Question question, string answerText, string lowerText, string jobContext)
        {
            if (string.IsNullOrWhiteSpace(answerText)) return 0m;

            var score = 0m;
            var answerWords = TokenizeText(lowerText);

            // 1. Phrase-based domain evidence (multi-word skills/techniques)
            var domainPhrases = GetDomainPhrases(question != null ? question.Text : "", jobContext);
            var matchedPhrases = domainPhrases.Where(p => lowerText.Contains(p)).Distinct().ToList();
            score += Math.Min(4m, matchedPhrases.Count * 0.6m);

            // 2. Action/result language density
            var actionPhrases = new[]
            {
                "responsible for", "led", "managed", "implemented", "delivered",
                "optimized", "improved", "trained", "mentored", "reduced", "increased"
            };
            var actionMatches = actionPhrases.Count(p => lowerText.Contains(p));
            score += Math.Min(2m, actionMatches * 0.4m);

            // 3. Evidence of measurable experience
            if (Regex.IsMatch(lowerText, @"\b\d+\+?\s*(years?|months?)\b", RegexOptions.IgnoreCase))
                score += 0.8m;
            if (Regex.IsMatch(lowerText, @"\b\d+\s*(clients?|customers?|people|appointments?|cuts|haircuts)\b", RegexOptions.IgnoreCase))
                score += 0.8m;
            if (Regex.IsMatch(lowerText, @"\b(certified|licensed|apprentice|journeyman)\b", RegexOptions.IgnoreCase))
                score += 0.6m;

            // 4. Context alignment with job description/responsibilities
            var contextKeywords = ExtractContextKeywords(jobContext);
            if (contextKeywords.Any() && answerWords.Any())
            {
                var uniqueAnswerWords = new HashSet<string>(answerWords);
                var contextMatches = contextKeywords.Count(k => uniqueAnswerWords.Contains(k));
                score += Math.Min(2m, contextMatches * 0.2m);
            }

            System.Diagnostics.Debug.WriteLine(string.Format("Semantic intensity score: {0} (Phrases: {1}, Actions: {2})", score, matchedPhrases.Count, actionMatches));
            return score;
        }

        /// <summary>
        /// Analyze technical and domain-specific indicators
        /// </summary>
        private decimal AnalyzeTechnicalIndicators(string lowerText)
        {
            var score = 0m;

            // Technical terminology
            var technicalTerms = new[] {
                "api", "database", "framework", "algorithm", "architecture",
                "scalability", "performance", "security", "testing", "deployment",
                "version control", "agile", "scrum", "devops", "cloud", "microservices"
            };

            var technicalMatches = technicalTerms.Count(term => lowerText.Contains(term));
            score += Math.Min(2m, technicalMatches * 0.3m);

            // Programming languages and tools
            var programmingTerms = new[] {
                "javascript", "python", "java", "c#", "sql", "html", "css",
                "react", "angular", "vue", "node", "dotnet", "aws", "azure",
                "docker", "kubernetes", "git", "github", "gitlab"
            };

            var programmingMatches = programmingTerms.Count(term => lowerText.Contains(term));
            score += Math.Min(1.5m, programmingMatches * 0.2m);

            System.Diagnostics.Debug.WriteLine(string.Format("Technical indicators score: {0} (Technical: {1}, Programming: {2})", score, technicalMatches, programmingMatches));
            return score;
        }

        /// <summary>
        /// Analyze structure and coherence
        /// </summary>
        private decimal AnalyzeStructureAndCoherence(string answerText, string lowerText)
        {
            var score = 0m;

            // Sentence structure analysis
            var sentences = answerText.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var words = TokenizeText(lowerText);
            var avgWordsPerSentence = sentences.Any() ? (double)words.Count / sentences.Length : 0;
            
            // Reward well-structured answers
            if (avgWordsPerSentence >= 12 && avgWordsPerSentence <= 25) score += 1m; // Ideal complexity
            else if (avgWordsPerSentence >= 8 && avgWordsPerSentence < 12) score += 0.7m;
            else if (avgWordsPerSentence >= 6 && avgWordsPerSentence < 8) score += 0.5m;
            else if (avgWordsPerSentence > 25) score -= 0.3m; // Too complex

            // Paragraph structure
            var paragraphs = answerText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (paragraphs.Length >= 2 && paragraphs.Length <= 4) score += 0.5m; // Good paragraphing

            // Transition words and logical flow
            var transitionWords = new[] { "however", "therefore", "furthermore", "moreover", "consequently", "additionally" };
            var transitionCount = transitionWords.Count(word => lowerText.Contains(word));
            score += Math.Min(0.5m, transitionCount * 0.1m);

            System.Diagnostics.Debug.WriteLine(string.Format("Structure coherence score: {0} (Avg words/sentence: {1:F1}, Paragraphs: {2})", score, avgWordsPerSentence, paragraphs.Length));
            return score;
        }

        /// <summary>
        /// Apply quality adjustments and penalties
        /// </summary>
        private decimal ApplyQualityAdjustments(decimal score, string answerText, string lowerText, string jobContext)
        {
            // 1. Repetition Penalty: Detect if candidate is just copying the Job Description
            if (!string.IsNullOrEmpty(jobContext))
            {
                // Split job description into long phrases (10+ words)
                var jobPhrases = jobContext.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => s.Split(' ').Length > 10)
                    .Select(s => s.Trim())
                    .ToList();

                int repetitionMatches = 0;
                foreach (var phrase in jobPhrases)
                {
                    if (lowerText.Contains(phrase)) repetitionMatches++;
                }

                if (repetitionMatches >= 2)
                {
                    score -= (repetitionMatches * 1.5m); // Heavy penalty for copy-pasting
                    System.Diagnostics.Debug.WriteLine(string.Format("Repetition Penalty Applied: {0} matches", repetitionMatches));
                }
            }

            // 2. Penalty for overly long answers
            if (answerText.Length > 1000) score -= 0.5m;
            if (answerText.Length > 2000) score -= 1m;

            // Penalty for poor grammar indicators
            if (answerText.Contains("  ")) score -= 0.2m; // Double spaces
            if (!System.Text.RegularExpressions.Regex.IsMatch(answerText, @"^[A-Z]")) score -= 0.3m; // No capital start
            if (lowerText.Count(char.IsLetter) < (double)answerText.Length * 0.6) score -= 0.8m; // Too many numbers/symbols

            // Penalty for repetitive content
            var words = TokenizeText(lowerText);
            var repetitionRatio = words.Count > 0
                ? ((double)words.Count - words.Distinct().Count()) / (double)words.Count
                : 0;
            if (repetitionRatio > 0.3) score -= 0.5m;

            // Bonus for well-formatted answers
            if (answerText.Contains("\n") && answerText.Split('\n').Length >= 2) score += 0.2m; // Uses line breaks

            return Math.Max(0, score);
        }

        /// <summary>
        /// Enhanced text tokenization
        /// </summary>
        private List<string> TokenizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            var matches = Regex.Matches(text, @"\b[\p{L}\p{N}']+\b");
            var tokens = new List<string>();

            foreach (Match match in matches)
            {
                var normalized = NormalizeToken(match.Value);
                if (normalized.Length > 1 && !StopWords.Contains(normalized))
                {
                    tokens.Add(normalized);
                }
            }

            return tokens;
        }

        /// <summary>
        /// Get industry-specific keywords based on question context
        /// </summary>
        private List<string> GetIndustryKeywords(string questionText, string jobContext = null)
        {
            var lowerQuestion = (questionText ?? string.Empty).ToLower();
            var lowerContext = (jobContext ?? string.Empty).ToLower();
            var combined = string.Format("{0} {1}", lowerQuestion, lowerContext);
            
            if (combined.Contains("software") || combined.Contains("developer") || combined.Contains("programming"))
            {
                return new List<string> { "coding", "programming", "development", "software", "application", "system", "algorithm", "database", "api", "framework" };
            }
            else if (combined.Contains("management") || combined.Contains("lead") || combined.Contains("team"))
            {
                return new List<string> { "leadership", "management", "team", "project", "strategy", "planning", "coordination", "supervision", "mentoring" };
            }
            else if (combined.Contains("sales") || combined.Contains("marketing") || combined.Contains("customer"))
            {
                return new List<string> { "sales", "marketing", "customer", "client", "revenue", "growth", "target", "negotiation", "relationship" };
            }
            else if (combined.Contains("design") || combined.Contains("creative") || combined.Contains("ux"))
            {
                return new List<string> { "design", "creative", "user", "experience", "interface", "visual", "prototype", "wireframe", "branding" };
            }
            else if (combined.Contains("barber") || combined.Contains("hair") || combined.Contains("salon") || combined.Contains("groom"))
            {
                return new List<string> { "barbering", "fade", "taper", "clippers", "scissors", "shear", "trim", "shave", "lineup", "beard", "styling", "texture", "blending", "sanitation", "razor", "haircut", "cutting", "grooming", "edge", "pomade", "clipper" };
            }
            
            // General business keywords
            return new List<string> { "business", "process", "improvement", "efficiency", "quality", "performance", "analysis", "solution" };
        }

        /// <summary>
        /// Get strong action verbs that indicate experience
        /// </summary>
        private List<string> GetStrongActionVerbs()
        {
            return new List<string> {
                "developed", "created", "built", "designed", "implemented", "managed", "led", "coordinated",
                "executed", "delivered", "achieved", "accomplished", "improved", "increased", "reduced",
                "optimized", "enhanced", "launched", "established", "transformed", "revolutionized",
                "pioneered", "innovated", "streamlined", "automated", "integrated", "migrated", "deployed"
            };
        }

        /// <summary>
        /// Get technology and specific skill keywords
        /// </summary>
        private List<string> GetSkillKeywords(string questionText, string jobContext = null)
        {
            var lowerQuestion = (questionText ?? string.Empty).ToLower();
            var lowerContext = (jobContext ?? string.Empty).ToLower();
            var combined = string.Format("{0} {1}", lowerQuestion, lowerContext);

            if (combined.Contains("barber") || combined.Contains("hair") || combined.Contains("salon") || combined.Contains("groom"))
            {
                return new List<string> {
                    "scissor", "clipper", "razor", "blade", "trimmer", "blowdryer", "shears", "comb", "brush",
                    "taper", "fade", "blend", "lineup", "straight", "texture", "layer", "shave", "guard",
                    "pomade", "gel", "wax", "sanitization", "barbicide", "sterilization", "hygiene", "clippers"
                };
            }

            // Default to software/tech keywords
            return new List<string> {
                "javascript", "python", "java", "csharp", "c++", "ruby", "php", "swift", "kotlin",
                "html", "css", "sql", "nosql", "mongodb", "postgresql", "mysql", "oracle",
                "react", "angular", "vue", "node", "express", "django", "flask", "spring", "dotnet",
                "aws", "azure", "gcp", "cloud", "docker", "kubernetes", "jenkins", "git", "github",
                "agile", "scrum", "devops", "ci", "cd", "testing", "unit", "integration", "api",
                "microservices", "architecture", "security", "performance", "scalability", "mobile"
            };
        }

        private List<string> GetDomainPhrases(string questionText, string jobContext)
        {
            var combined = string.Format("{0} {1}", questionText ?? "", jobContext ?? "").ToLower();
            var phrases = new List<string>();

            if (combined.Contains("barber") || combined.Contains("hair") || combined.Contains("salon") || combined.Contains("groom"))
            {
                phrases.AddRange(new[]
                {
                    "skin fade", "low fade", "high fade", "mid fade", "taper fade",
                    "line up", "line-up", "beard lineup", "straight razor", "hot towel",
                    "shear over comb", "clipper over comb", "texturizing shears",
                    "beard trim", "beard shaping", "neck shave", "razor shave"
                });
            }

            if (combined.Contains("developer") || combined.Contains("software") || combined.Contains("programming"))
            {
                phrases.AddRange(new[]
                {
                    "unit testing", "code review", "continuous integration",
                    "api design", "database migration", "performance tuning"
                });
            }

            // Generic phrases that indicate concrete experience
            phrases.AddRange(new[]
            {
                "for example", "for instance", "in my role", "as a", "responsible for"
            });

            return phrases.Distinct().ToList();
        }

        /// <summary>
        /// Extract keywords from question text
        /// </summary>
        private List<string> ExtractQuestionKeywords(string questionText)
        {
            return TokenizeText(questionText)
                     .Where(word => word.Length > 2)
                     .Distinct()
                     .ToList();
        }

        private string NormalizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return string.Empty;

            var normalized = token.Trim('\'').ToLowerInvariant();

            // Basic suffix stripping for better matching (light stemming)
            if (normalized.Length > 4 && normalized.EndsWith("ing"))
                normalized = normalized.Substring(0, normalized.Length - 3);
            else if (normalized.Length > 3 && normalized.EndsWith("ed"))
                normalized = normalized.Substring(0, normalized.Length - 2);
            else if (normalized.Length > 3 && normalized.EndsWith("es"))
                normalized = normalized.Substring(0, normalized.Length - 2);
            else if (normalized.Length > 3 && normalized.EndsWith("s"))
                normalized = normalized.Substring(0, normalized.Length - 1);

            return normalized;
        }

        private decimal CalculateSpecificityBonus(Question question, List<string> answerWords, string jobContext)
        {
            if (question == null || answerWords == null || answerWords.Count == 0) return 0m;

            var skillKeywords = GetSkillKeywords(question.Text, jobContext)
                .Select(NormalizeToken)
                .Where(k => !string.IsNullOrWhiteSpace(k));
            var industryKeywords = GetIndustryKeywords(question.Text, jobContext)
                .Select(NormalizeToken)
                .Where(k => !string.IsNullOrWhiteSpace(k));
            var questionKeywords = ExtractQuestionKeywords(question.Text);
            var contextKeywords = ExtractContextKeywords(jobContext);

            var targetKeywords = new HashSet<string>(skillKeywords
                .Concat(industryKeywords)
                .Concat(questionKeywords)
                .Concat(contextKeywords));

            if (targetKeywords.Count == 0) return 0m;

            var uniqueAnswers = answerWords.Distinct().ToList();
            var uniqueMatches = uniqueAnswers.Count(w => targetKeywords.Contains(w));
            var density = uniqueAnswers.Count > 0 ? (decimal)uniqueMatches / uniqueAnswers.Count : 0m;

            var score = Math.Min(3m, uniqueMatches * 0.4m);
            if (density >= 0.25m) score += 1.0m;
            else if (density >= 0.15m) score += 0.6m;
            else if (density >= 0.08m) score += 0.3m;

            return score;
        }

        private List<string> ExtractContextKeywords(string jobContext)
        {
            if (string.IsNullOrWhiteSpace(jobContext)) return new List<string>();

            var tokens = TokenizeText(jobContext);
            var grouped = tokens
                .GroupBy(t => t)
                .Select(g => new { Token = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Token)
                .Take(20)
                .Select(g => g.Token)
                .ToList();

            return grouped;
        }

        /// <summary>
        /// Calculate semantic matches between answer and question keywords
        /// </summary>
        private int CalculateSemanticMatches(List<string> answerWords, List<string> questionKeywords)
        {
            var matches = 0;
            
            foreach (var answerWord in answerWords)
            {
                foreach (var questionKeyword in questionKeywords)
                {
                    // Direct match
                    if (answerWord.Equals(questionKeyword, StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                        break;
                    }
                    
                    // Partial match (word contains keyword or vice versa)
                    if (answerWord.Length > 4 && questionKeyword.Length > 4)
                    {
                        if (answerWord.Contains(questionKeyword) || questionKeyword.Contains(answerWord))
                        {
                            matches++;
                            break;
                        }
                    }
                    
                    // Levenshtein distance for fuzzy matching (simplified)
                    if (CalculateLevenshteinDistance(answerWord, questionKeyword) <= 2)
                    {
                        matches++;
                        break;
                    }
                }
            }
            
            return matches;
        }

        /// <summary>
        /// Simple Levenshtein distance calculation
        /// </summary>
        private int CalculateLevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return string.IsNullOrEmpty(s2) ? 0 : s2.Length;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            var matrix = new int[s1.Length + 1, s2.Length + 1];

            for (var i = 0; i <= s1.Length; i++)
                matrix[i, 0] = i;
            for (var j = 0; j <= s2.Length; j++)
                matrix[0, j] = j;

            for (var i = 1; i <= s1.Length; i++)
            {
                for (var j = 1; j <= s2.Length; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[s1.Length, s2.Length];
        }

        /// <summary>
        /// Calculate question type specific relevance
        /// </summary>
        private decimal CalculateQuestionTypeRelevance(Question question, string answerText, string lowerText)
        {
            var score = 0m;
            var lowerQuestion = question.Text.ToLower();
            
            // Experience-related questions
            if (lowerQuestion.Contains("experience") || lowerQuestion.Contains("background") || lowerQuestion.Contains("history"))
            {
                var experiencePatterns = new[] {
                    @"\d+\s+(years?|months?)\s+(of\s+)?experience",
                    @"worked\s+(as|with|for)",
                    @"previous\s+(role|position|job)",
                    @"background\s+in"
                };
                
                var experienceCount = experiencePatterns.Count(pattern => 
                    System.Text.RegularExpressions.Regex.IsMatch(lowerText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
                score += Math.Min(2m, experienceCount * 0.7m);
            }
            
            // Skills-related questions
            else if (lowerQuestion.Contains("skill") || lowerQuestion.Contains("ability") || lowerQuestion.Contains("knowledge"))
            {
                var skillIndicators = new[] { "proficient", "expert", "skilled", "knowledge", "familiar", "experienced", "certified" };
                var skillCount = skillIndicators.Count(indicator => lowerText.Contains(indicator));
                score += Math.Min(1.5m, skillCount * 0.3m);
            }
            
            // Problem-solving questions
            else if (lowerQuestion.Contains("challenge") || lowerQuestion.Contains("problem") || lowerQuestion.Contains("solve"))
            {
                var solutionPatterns = new[] {
                    @"solved\s+(the|a|this)",
                    @"approach\s+(was|included)",
                    @"solution\s+(was|involved)",
                    @"resolved\s+(the|this|issue)"
                };
                
                var solutionCount = solutionPatterns.Count(pattern => 
                    System.Text.RegularExpressions.Regex.IsMatch(lowerText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
                score += Math.Min(1.5m, solutionCount * 0.5m);
            }
            
            return score;
        }

        /// <summary>
        /// Calculate score for choice questions with AI enhancement
        /// </summary>
        private decimal CalculateChoiceScore(Question question, string answerText, int positionId)
        {
            return CalculateChoiceScoreFallback(question, answerText, positionId);
        }

        /// <summary>
        /// Get available options for a question
        /// </summary>
        private List<string> GetQuestionOptions(int questionId)
        {
            return _uow.Context.Set<HR.Web.Models.QuestionOption>()
                .Where(qo => qo.QuestionId == questionId)
                .Select(qo => qo.Text)
                .ToList();
        }

        /// <summary>
        /// Get maximum possible score for a position
        /// </summary>
        public decimal GetMaxScoreForPosition(int positionId)
        {
            var positionQuestions = _uow.Context.Set<PositionQuestion>()
                .Where(pq => pq.PositionId == positionId)
                .Include(pq => pq.Question)
                .ToList();

            return GetMaxPossibleScoreForPosition(positionQuestions);
        }

        /// <summary>
        /// Get maximum possible score for a question
        /// </summary>
        public decimal GetMaxScoreForQuestion(Question question, int positionId)
        {
            switch (question.Type.ToLower())
            {
                case "choice":
                    return GetMaxChoiceScore(question, positionId);
                case "rating":
                    return 10; // 1-5 rating scale -> max 10 points
                case "number":
                    return 10; // Scaled to max 10 points
                case "text":
                    return 30; // Max 30 points for text answers (matches GetMaxPossibleScoreForPosition)
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Get maximum score for choice questions
        /// </summary>
        private decimal GetMaxChoiceScore(Question question, int positionId)
        {
            // Check for position-specific overrides
            var positionQuestion = _uow.Context.Set<PositionQuestion>()
                .FirstOrDefault(pq => pq.PositionId == positionId && pq.QuestionId == question.Id);

            if (positionQuestion != null)
            {
                var positionOptions = _uow.Context.Set<PositionQuestionOption>()
                    .Where(pqo => pqo.PositionQuestionId == positionQuestion.Id)
                    .ToList();

                if (positionOptions.Any())
                {
                    return positionOptions.Where(pqo => pqo.Points.HasValue)
                        .Max(pqo => pqo.Points.Value);
                }
            }

            // Fallback to default options
            var defaultOptions = _uow.Context.Set<HR.Web.Models.QuestionOption>()
                .Where(qo => qo.QuestionId == question.Id)
                .ToList();

            return defaultOptions.Any() ? defaultOptions.Max(o => o.Points) : 0;
        }

        /// <summary>
        /// Get score breakdown for an application
        /// </summary>
        public List<QuestionScoreBreakdown> GetScoreBreakdown(int applicationId)
        {
            var application = _uow.Applications.Get(applicationId);
            if (application == null) return new List<QuestionScoreBreakdown>();

            var breakdown = new List<QuestionScoreBreakdown>();

            var positionQuestions = _uow.Context.Set<PositionQuestion>()
                .Where(pq => pq.PositionId == application.PositionId)
                .Include(pq => pq.Question)
                .OrderBy(pq => pq.Order)
                .ToList();

            var effectiveWeights = GetEffectiveQuestionWeights(positionQuestions);

            var answers = _uow.Context.Set<ApplicationAnswer>()
                .Where(aa => aa.ApplicationId == application.Id)
                .ToList();

            foreach (var positionQuestion in positionQuestions)
            {
                var answer = answers.FirstOrDefault(a => a.QuestionId == positionQuestion.QuestionId);
                var rawScore = answer != null ?
                    CalculateQuestionScore(positionQuestion.Question, answer.AnswerText, application.PositionId) : 0;
                var rawMaxScore = GetMaxScoreForQuestion(positionQuestion.Question, application.PositionId);
                decimal weight;
                if (!effectiveWeights.TryGetValue(positionQuestion.Id, out weight))
                {
                    weight = 0m;
                }
                var normalizedScore = rawMaxScore > 0 ? Clamp01(rawScore / rawMaxScore) : 0m;
                var weightedScore = normalizedScore * weight;

                breakdown.Add(new QuestionScoreBreakdown
                {
                    QuestionId = positionQuestion.QuestionId,
                    QuestionText = positionQuestion.Question.Text,
                    QuestionType = positionQuestion.Question.Type,
                    Order = positionQuestion.Order,
                    Answer = answer != null ? answer.AnswerText : "Not answered",
                    Weight = weight,
                    Score = weightedScore,
                    MaxScore = weight,
                    Percentage = normalizedScore * 100m
                });
            }

            return breakdown;
        }

        /// <summary>
        /// Rank candidates for a position
        /// </summary>
        public List<CandidateRanking> RankCandidatesForPosition(int positionId)
        {
            var applications = _uow.Applications.GetAll(
                a => a.Applicant,
                a => a.Position
            ).Where(a => a.PositionId == positionId).ToList();

            var rankings = new List<CandidateRanking>();

            foreach (var application in applications)
            {
                var percentage = CalculateApplicationScore(application); // This now returns percentage
                var breakdown = GetScoreBreakdown(application.Id);

                rankings.Add(new CandidateRanking
                {
                    ApplicationId = application.Id,
                    CandidateName = application.Applicant != null ? application.Applicant.FullName : null ?? "Unknown",
                    CandidateEmail = application.Applicant != null ? application.Applicant.Email : null ?? "",
                    TotalScore = percentage, // This is now percentage out of 100
                    MaxScore = 100, // Always 100 for percentage system
                    Percentage = percentage, // Same as TotalScore now
                    AppliedDate = application.AppliedOn,
                    Status = application.Status ?? "Pending",
                    ScoreBreakdown = breakdown
                });
            }

            return rankings.OrderByDescending(r => r.Percentage).ToList();
        }

        /// <summary>
        /// Analyze question performance
        /// </summary>
        public async Task<QuestionPerformanceAnalysis> AnalyzeQuestionPerformance(int questionId)
        {
            var question = _uow.Questions.Get(questionId);
            if (question == null) return null;

            // Get all answers for this question
            var answers = _uow.Context.Set<ApplicationAnswer>()
                .Where(aa => aa.QuestionId == questionId)
                .ToList();

            if (!answers.Any()) return null;

            // Calculate score distribution
            var scoreDistribution = new Dictionary<string, int>();
            var totalScore = 0m;

            foreach (var answer in answers)
            {
                var application = _uow.Applications.Get(answer.ApplicationId);
                if (application != null)
                {
                    var score = CalculateQuestionScore(question, answer.AnswerText, application.PositionId);
                    totalScore += score;
                    
                    var scoreRange = GetScoreRange(score);
                    int current = 0;
                    scoreDistribution.TryGetValue(scoreRange, out current);
                    scoreDistribution[scoreRange] = current + 1;
                }
            }

            var averageScore = answers.Count > 0 ? totalScore / answers.Count : 0;

            // Use MCP to get additional insights
            var mcpAnalysis = await GetMCPQuestionAnalysis(questionId, scoreDistribution, (double)averageScore, answers.Count);

            return new QuestionPerformanceAnalysis
            {
                QuestionId = questionId,
                QuestionText = question.Text,
                TotalResponses = answers.Count,
                AverageScore = averageScore,
                ScoreDistribution = scoreDistribution,
                MCPAnalysis = mcpAnalysis
            };
        }

        private async Task<object> GetMCPQuestionAnalysis(int questionId, Dictionary<string, int> distribution, double averageScore, int totalResponses)
        {
            try
            {
                var parameters = new
                {
                    questionId = questionId.ToString(),
                    responseDistribution = distribution,
                    averageScore = averageScore,
                    totalResponses = totalResponses
                };

                var response = await _mcpService.CallToolAsync("analyze-performance", parameters);
                
                if (response.Success)
                {
                    var content = response.Result.contents[0];
                    return JsonConvert.DeserializeObject<dynamic>(content.text);
                }
            }
            catch
            {
                // Fallback if MCP is not available
            }

            return null;
        }

        private string GetScoreRange(decimal score)
        {
            if (score <= 2) return "0-2";
            if (score <= 4) return "3-4";
            if (score <= 6) return "5-6";
            if (score <= 8) return "7-8";
            return "9-10";
        }
    }

    // Supporting classes
    public class QuestionScoreBreakdown
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string QuestionType { get; set; }
        public int Order { get; set; }
        public string Answer { get; set; }
        public decimal Weight { get; set; }
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
    }

    public class CandidateRanking
    {
        public int ApplicationId { get; set; }
        public string CandidateName { get; set; }
        public string CandidateEmail { get; set; }
        public decimal TotalScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
        public DateTime AppliedDate { get; set; }
        public string Status { get; set; }
        public List<QuestionScoreBreakdown> ScoreBreakdown { get; set; }
    }

    public class QuestionPerformanceAnalysis
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public int TotalResponses { get; set; }
        public decimal AverageScore { get; set; }
        public Dictionary<string, int> ScoreDistribution { get; set; }
        public dynamic MCPAnalysis { get; set; }
    }

    // Response model for AI answer evaluation
    public class AnswerEvaluationResponse
    {
        public decimal score { get; set; } // 0-10 scale
        public string quality { get; set; } // excellent, good, fair, poor
        public List<string> strengths { get; set; }
        public List<string> weaknesses { get; set; }
        public List<string> issues { get; set; } // grammar, spelling, relevance, etc.
        public string reasoning { get; set; }
        public bool isPlagiarized { get; set; }
        public decimal confidence { get; set; } // AI confidence in evaluation
    }
}
