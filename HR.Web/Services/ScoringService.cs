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
            var rawScore = 0m;

            // Get position questions and their order
            var positionQuestions = _uow.Context.Set<PositionQuestion>()
                .Where(pq => pq.PositionId == application.PositionId)
                .Include(pq => pq.Question)
                .OrderBy(pq => pq.Order)
                .ToList();

            // Get application answers
            var answers = _uow.Context.Set<ApplicationAnswer>()
                .Where(aa => aa.ApplicationId == application.Id)
                .ToList();

            foreach (var positionQuestion in positionQuestions)
            {
                var answer = answers.FirstOrDefault(a => a.QuestionId == positionQuestion.QuestionId);
                if (answer != null)
                {
                    rawScore += CalculateQuestionScore(positionQuestion.Question, answer.AnswerText, application.PositionId);
                }
            }

            // Calculate maximum possible score for this position
            var maxPossibleScore = GetMaxPossibleScoreForPosition(positionQuestions);
            
            // Convert to percentage out of 100
            var percentage = maxPossibleScore > 0 ? (rawScore / maxPossibleScore) * 100 : 0;
            
            return Math.Max(0, Math.Min(100, percentage)); // Cap at 100%
        }

        /// <summary>
        /// Get maximum possible score for a position based on question types
        /// </summary>
        private decimal GetMaxPossibleScoreForPosition(List<PositionQuestion> positionQuestions)
        {
            var maxScore = 0m;
            
            foreach (var positionQuestion in positionQuestions)
            {
                switch (positionQuestion.Question.Type.ToLower())
                {
                    case "choice":
                        maxScore += GetMaxChoiceScore(positionQuestion.Question, positionQuestion.PositionId);
                        break;
                    case "rating":
                        maxScore += 10; // Rating questions max 10 points
                        break;
                    case "number":
                        maxScore += 10; // Number questions max 10 points
                        break;
                    case "text":
                        maxScore += 30; // Text questions can now score up to ~30 points with enhanced algorithm
                        break;
                    default:
                        maxScore += 0;
                        break;
                }
            }
            
            return maxScore;
        }

        /// <summary>
        /// Calculate score for a single question using MCPService for content-based evaluation
        /// </summary>
        public decimal CalculateQuestionScore(Question question, string answerText, int positionId)
        {
            if (string.IsNullOrEmpty(answerText)) return 0;

            try
            {
                // Use MCPService for content-based scoring
                var mcpService = new MCPService();
                var parameters = new Dictionary<string, object>
                {
                    { "questionText", question.Text },
                    { "selectedAnswer", answerText },
                    { "questionType", question.Type },
                    { "maxPoints", GetMaxScoreForQuestion(question, positionId) }
                };

                var result = mcpService.CallToolAsync("evaluate-answer", parameters).Result;
                
                if (result.Success && result.Result != null)
                {
                    var content = result.Result.contents[0];
                    var evaluation = JsonConvert.DeserializeObject<AnswerEvaluationResponse>(content.text);
                    
                    // Log the content-based scoring for debugging
                    System.Diagnostics.Debug.WriteLine($"MCPService Score - Question: {question.Text.Substring(0, Math.Min(50, question.Text.Length))}..., Answer: {answerText.Substring(0, Math.Min(30, answerText.Length))}..., Score: {evaluation.score}");
                    
                    return evaluation.score;
                }
                else
                {
                    // Fallback to traditional scoring if MCPService fails
                    System.Diagnostics.Debug.WriteLine($"MCPService failed for question {question.Id}, using fallback scoring");
                    return CalculateQuestionScoreFallback(question, answerText, positionId);
                }
            }
            catch (Exception ex)
            {
                // Log error and use fallback
                System.Diagnostics.Debug.WriteLine($"Error in MCPService scoring: {ex.Message}, using fallback");
                return CalculateQuestionScoreFallback(question, answerText, positionId);
            }
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
            try
            {
                // Use AI to validate and contextualize rating answers
                var parameters = new
                {
                    questionText = question.Text,
                    ratingValue = answerText,
                    questionType = "rating",
                    maxPoints = 10,
                    context = "1-5 scale" // Inform AI about expected scale
                };

                var callTask = _mcpService.CallToolAsync("evaluate-answer", parameters);
                var completed = Task.WhenAny(callTask, Task.Delay(1500));
                
                if (completed.Result == callTask && callTask.Result.Success)
                {
                    var content = callTask.Result.Result.contents[0];
                    var evaluation = JsonConvert.DeserializeObject<AnswerEvaluationResponse>(content.text);
                    
                    // Use AI-validated score if reasonable
                    if (evaluation.score >= 0 && evaluation.score <= 10)
                    {
                        return evaluation.score;
                    }
                }
            }
            catch
            {
                // Fall through to existing logic
            }

            // Existing rating logic as fallback
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
            try
            {
                // Use AI to validate and contextualize numeric answers
                var parameters = new
                {
                    questionText = question.Text,
                    numericValue = answerText,
                    questionType = "number",
                    maxPoints = 10,
                    context = ExtractQuestionContext(question.Text)
                };

                var callTask = _mcpService.CallToolAsync("evaluate-answer", parameters);
                var completed = Task.WhenAny(callTask, Task.Delay(1500));
                
                if (completed.Result == callTask && callTask.Result.Success)
                {
                    var content = callTask.Result.Result.contents[0];
                    var evaluation = JsonConvert.DeserializeObject<AnswerEvaluationResponse>(content.text);
                    
                    // Use AI-validated score if reasonable
                    if (evaluation.score >= 0 && evaluation.score <= 10)
                    {
                        return evaluation.score;
                    }
                }
            }
            catch
            {
                // Fall through to existing logic
            }

            // Existing number logic as fallback
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
            score += ExtractAndScoreKeywords(question, answerText, lowerText);

            // 3. Experience Intensity and Answer Strength
            score += AnalyzeAnswerStrength(answerText, lowerText);

            // 4. Professional communication
            score += AnalyzeProfessionalCommunication(lowerText);

            // 5. Contextual relevance
            score += AnalyzeContextualRelevance(question, answerText, lowerText);

            // 6. Technical indicators
            score += AnalyzeTechnicalIndicators(lowerText);

            // 7. Structure and coherence
            score += AnalyzeStructureAndCoherence(answerText, lowerText);

            // 8. Quality penalties (including Repetition Penalty)
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
        private decimal ExtractAndScoreKeywords(Question question, string answerText, string lowerText)
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
            var industryKeywords = GetIndustryKeywords(question.Text);
            var industryMatches = words.Count(w => industryKeywords.Contains(w));
            score += Math.Min(2m, industryMatches * 0.4m);

            // Action verb detection (strong indicators of experience)
            var actionVerbs = GetStrongActionVerbs();
            var actionMatches = words.Count(w => actionVerbs.Contains(w));
            score += Math.Min(1.5m, actionMatches * 0.3m);

            // Skill and technology detection
            var techKeywords = GetTechnologyKeywords();
            var techMatches = words.Count(w => techKeywords.Contains(w));
            score += Math.Min(2m, techMatches * 0.5m);

            System.Diagnostics.Debug.WriteLine(string.Format("Keyword extraction score: {0} (Industry: {1}, Actions: {2}, Tech: {3})", score, industryMatches, actionMatches, techMatches));
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
            var repetitionRatio = (double)words.Count - words.Distinct().Count() / (double)words.Count;
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
            return text.Split(new[] { ' ', '.', ',', ';', ':', '!', '?', '-', '_', '/', '\\', '(', ')', '[', ']', '{', '}', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                     .Where(word => word.Length > 1) // Filter out single characters
                     .ToList();
        }

        /// <summary>
        /// Get industry-specific keywords based on question context
        /// </summary>
        private List<string> GetIndustryKeywords(string questionText)
        {
            var lowerQuestion = questionText.ToLower();
            
            if (lowerQuestion.Contains("software") || lowerQuestion.Contains("developer") || lowerQuestion.Contains("programming"))
            {
                return new List<string> { "coding", "programming", "development", "software", "application", "system", "algorithm", "database", "api", "framework" };
            }
            else if (lowerQuestion.Contains("management") || lowerQuestion.Contains("lead") || lowerQuestion.Contains("team"))
            {
                return new List<string> { "leadership", "management", "team", "project", "strategy", "planning", "coordination", "supervision", "mentoring" };
            }
            else if (lowerQuestion.Contains("sales") || lowerQuestion.Contains("marketing") || lowerQuestion.Contains("customer"))
            {
                return new List<string> { "sales", "marketing", "customer", "client", "revenue", "growth", "target", "negotiation", "relationship" };
            }
            else if (lowerQuestion.Contains("design") || lowerQuestion.Contains("creative") || lowerQuestion.Contains("ux"))
            {
                return new List<string> { "design", "creative", "user", "experience", "interface", "visual", "prototype", "wireframe", "branding" };
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
        /// Get technology and skill keywords
        /// </summary>
        private List<string> GetTechnologyKeywords()
        {
            return new List<string> {
                "javascript", "python", "java", "csharp", "c++", "ruby", "php", "swift", "kotlin",
                "html", "css", "sql", "nosql", "mongodb", "postgresql", "mysql", "oracle",
                "react", "angular", "vue", "node", "express", "django", "flask", "spring", "dotnet",
                "aws", "azure", "gcp", "cloud", "docker", "kubernetes", "jenkins", "git", "github",
                "agile", "scrum", "devops", "ci", "cd", "testing", "unit", "integration", "api",
                "microservices", "architecture", "security", "performance", "scalability", "mobile"
            };
        }

        /// <summary>
        /// Extract keywords from question text
        /// </summary>
        private List<string> ExtractQuestionKeywords(string questionText)
        {
            var stopWords = new[] { "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "is", "are", "was", "were", "be", "been", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may", "might", "can", "what", "how", "when", "where", "why", "describe", "explain", "tell", "your" };
            
            return TokenizeText(questionText.ToLower())
                     .Where(word => !stopWords.Contains(word) && word.Length > 2)
                     .Distinct()
                     .ToList();
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
            try
            {
                // Use AI to validate and score choice answers
                var parameters = new
                {
                    questionText = question.Text,
                    selectedAnswer = answerText,
                    questionType = "choice",
                    availableOptions = GetQuestionOptions(question.Id),
                    maxPoints = 10
                };

                var callTask = _mcpService.CallToolAsync("evaluate-answer", parameters);
                var completed = Task.WhenAny(callTask, Task.Delay(2000));
                
                if (completed.Result == callTask && callTask.Result.Success)
                {
                    var content = callTask.Result.Result.contents[0];
                    var evaluation = JsonConvert.DeserializeObject<AnswerEvaluationResponse>(content.text);
                    
                    // Use AI score if available and reasonable
                    if (evaluation.score >= 0 && evaluation.score <= 10)
                    {
                        return evaluation.score;
                    }
                }
            }
            catch
            {
                // Fall through to existing logic
            }

            // Existing choice scoring logic as fallback
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

            var answers = _uow.Context.Set<ApplicationAnswer>()
                .Where(aa => aa.ApplicationId == application.Id)
                .ToList();

            foreach (var positionQuestion in positionQuestions)
            {
                var answer = answers.FirstOrDefault(a => a.QuestionId == positionQuestion.QuestionId);
                var score = answer != null ? 
                    CalculateQuestionScore(positionQuestion.Question, answer.AnswerText, application.PositionId) : 0;
                var maxScore = GetMaxScoreForQuestion(positionQuestion.Question, application.PositionId);

                breakdown.Add(new QuestionScoreBreakdown
                {
                    QuestionId = positionQuestion.QuestionId,
                    QuestionText = positionQuestion.Question.Text,
                    QuestionType = positionQuestion.Question.Type,
                    Order = positionQuestion.Order,
                    Answer = answer != null ? answer.AnswerText : null ?? "Not answered",
                    Score = score,
                    MaxScore = maxScore,
                    Percentage = maxScore > 0 ? (score / maxScore) * 100 : 0
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
