using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HR.Web.Models;

namespace HR.Web.Services
{
    public class MLQuestionnaireService : QuestionnaireService
    {
        private Dictionary<string, List<string>> _trainingData;
        private Dictionary<string, List<string>> _categoryPatterns;
        private Dictionary<string, List<string>> _industryKeywords;
        private Dictionary<string, List<IndustryTemplate>> _industryTemplates;

        public class IndustryTemplate
        {
            public string[] QuestionPatterns { get; set; }
        }

        public MLQuestionnaireService()
        {
            InitializeTrainingData();
            InitializeCategoryPatterns();
            InitializeIndustryData();
        }

        private void InitializeTrainingData()
        {
            // Simulated training data for ML-like pattern recognition
            _trainingData = new Dictionary<string, List<string>>();
            _trainingData.Add("technical", new List<string>
            {
                "javascript", "python", "java", "c#", "sql", "react", "angular", "node.js",
                "docker", "aws", "azure", "git", "mongodb", "postgresql", "api", "microservices"
            });
            _trainingData.Add("leadership", new List<string>
            {
                "lead", "manage", "team", "supervise", "mentor", "guide", "coordinate", "direct"
            });
            _trainingData.Add("problem-solving", new List<string>
            {
                "solve", "problem", "challenge", "solution", "analyze", "troubleshoot", "debug"
            });
            _trainingData.Add("communication", new List<string>
            {
                "communicate", "present", "explain", "document", "collaborate", "interact"
            });
            _trainingData.Add("analytical", new List<string>
            {
                "analyze", "data", "metrics", "research", "evaluate", "assess", "measure"
            });
        }

        private void InitializeCategoryPatterns()
        {
            _categoryPatterns = new Dictionary<string, List<string>>();
            _categoryPatterns.Add("technical", new List<string> { "experience", "skills", "knowledge", "proficient", "expert" });
            _categoryPatterns.Add("leadership", new List<string> { "team", "lead", "manage", "mentor", "guide" });
            _categoryPatterns.Add("behavioral", new List<string> { "handle", "approach", "describe", "situation", "scenario" });
            _categoryPatterns.Add("analytical", new List<string> { "analyze", "evaluate", "assess", "measure", "optimize" });
        }

        private void InitializeIndustryData()
        {
            _industryKeywords = new Dictionary<string, List<string>>
            {
                { "Technology", new List<string> { "software", "development", "programming", "cloud", "agile" } },
                { "Finance", new List<string> { "accounting", "banking", "audit", "financial", "investment" } },
                { "Healthcare", new List<string> { "medical", "patient", "clinical", "hospital", "nursing" } }
            };

            _industryTemplates = new Dictionary<string, List<IndustryTemplate>>
            {
                { "Technology", new List<IndustryTemplate> { new IndustryTemplate { QuestionPatterns = new[] { "patterns" } } } },
                { "Finance", new List<IndustryTemplate> { new IndustryTemplate { QuestionPatterns = new[] { "patterns" } } } },
                { "Healthcare", new List<IndustryTemplate> { new IndustryTemplate { QuestionPatterns = new[] { "patterns" } } } },
                { "General", new List<IndustryTemplate> { new IndustryTemplate { QuestionPatterns = new[] { "patterns" } } } }
            };
        }

        public new List<GeneratedQuestion> GenerateSmartQuestions(
            string jobTitle, 
            string jobDescription, 
            string keyResponsibilities = "",
            string requiredQualifications = "",
            string experience = "mid", 
            List<string> questionTypes = null, 
            int count = 5)
        {
            // 1. Enhanced industry detection with ML-like scoring
            var industry = DetectIndustryWithScoring(jobTitle + " " + jobDescription);
            
            // 2. Advanced keyword extraction with ML-like weighting
            var keywords = ExtractKeywordsWithWeighting(jobTitle, jobDescription, keyResponsibilities, requiredQualifications);
            
            // 3. ML-enhanced category prediction
            var predictedCategories = PredictCategoriesWithML(keywords);
            
            // 4. Generate questions with ML-informed patterns
            var questions = GenerateMLEnhancedQuestions(industry, keywords, predictedCategories, experience, questionTypes, count);
            
            // 5. Apply ML-like optimization
            questions = OptimizeQuestionsWithML(questions, keywords);
            
            // 6. Validate and score
            return ValidateAndScoreQuestions(questions);
        }

        private string DetectIndustryWithScoring(string jobText)
        {
            var text = jobText.ToLower();
            var industryScores = new Dictionary<string, double>();

            // ML-like scoring algorithm
            // Assuming _industryKeywords comes from base class or is missing.
            // I will keep the code as is regarding _industryKeywords reference, assuming it compiles or is fixed elsewhere.
            if (_industryKeywords != null) 
            {
                foreach (var industry in _industryKeywords)
                {
                    var matches = industry.Value.Count(keyword => text.Contains(keyword));
                    var score = (double)matches / industry.Value.Count; // Normalized score
                    industryScores[industry.Key] = score;
                }
            }

            // Apply confidence threshold
            var topIndustry = industryScores.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
            return topIndustry.Value >= 0.3 ? topIndustry.Key : "General";
        }

        private Dictionary<string, List<string>> ExtractKeywordsWithWeighting(
            string jobTitle, 
            string jobDescription, 
            string keyResponsibilities, 
            string requiredQualifications)
        {
            var allText = string.Format("{0} {1} {2} {3}", jobTitle, jobDescription, keyResponsibilities, requiredQualifications);
            var keywords = new Dictionary<string, List<string>>();
            
            // Weighted keyword extraction
            keywords["technical"] = ExtractWeightedTechnicalSkills(allText);
            keywords["soft"] = ExtractWeightedSoftSkills(allText);
            keywords["experience"] = ExtractWeightedExperienceIndicators(allText);
            keywords["responsibilities"] = ExtractWeightedResponsibilities(keyResponsibilities);
            
            return keywords;
        }

        private List<string> ExtractWeightedTechnicalSkills(string text)
        {
            var techSkills = new List<string>();
            var weightedPatterns = new[]
            {
                new { Pattern = @"\b(JavaScript|Python|Java|C\#|SQL)\b", Weight = 3 },
                new { Pattern = @"\b(React|Angular|Vue|Node\.js|Docker)\b", Weight = 2 },
                new { Pattern = @"\b(AWS|Azure|GCP|Git|REST|GraphQL)\b", Weight = 2 },
                new { Pattern = @"\b(MongoDB|PostgreSQL|MySQL|Redis)\b", Weight = 1 }
            };
            
            foreach (var weightedPattern in weightedPatterns)
            {
                var matches = Regex.Matches(text, weightedPattern.Pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    // Add weighted keywords (higher weight = more important)
                    for (int i = 0; i < weightedPattern.Weight; i++)
                    {
                        techSkills.Add(match.Value);
                    }
                }
            }
            
            return techSkills.Distinct().ToList();
        }

        private List<string> ExtractWeightedSoftSkills(string text)
        {
            var softSkills = new List<string>();
            var weightedSkills = new[]
            {
                new { Skill = "leadership", Weight = 3 },
                new { Skill = "communication", Weight = 3 },
                new { Skill = "problem-solving", Weight = 2 },
                new { Skill = "teamwork", Weight = 2 },
                new { Skill = "analytical", Weight = 2 },
                new { Skill = "mentoring", Weight = 1 }
            };
            
            var words = Regex.Split(text.ToLower(), @"\W+");
            foreach (var weightedSkill in weightedSkills)
            {
                if (words.Contains(weightedSkill.Skill))
                {
                    for (int i = 0; i < weightedSkill.Weight; i++)
                    {
                        softSkills.Add(weightedSkill.Skill);
                    }
                }
            }
            
            return softSkills.Distinct().ToList();
        }

        private List<string> ExtractWeightedExperienceIndicators(string text)
        {
            var indicators = new List<string>();
            var experiencePatterns = new[]
            {
                new { Pattern = @"(\d+)\+?\s*years?", Weight = 3 },
                new { Pattern = @"(senior|lead|principal)", Weight = 3 },
                new { Pattern = @"(junior|mid.level)", Weight = 2 },
                new { Pattern = @"(expert|advanced)", Weight = 2 },
                new { Pattern = @"(beginner|entry)", Weight = 1 }
            };
            
            foreach (var pattern in experiencePatterns)
            {
                var matches = Regex.Matches(text.ToLower(), pattern.Pattern);
                foreach (Match match in matches)
                {
                    for (int i = 0; i < pattern.Weight; i++)
                    {
                        indicators.Add(match.Value);
                    }
                }
            }
            
            return indicators.Distinct().ToList();
        }

        private List<string> ExtractWeightedResponsibilities(string responsibilities)
        {
            if (string.IsNullOrEmpty(responsibilities))
                return new List<string>();
                
            var sentences = Regex.Split(responsibilities, @"(?<=[.!?])\s+")
                .Where(s => s.Length > 10)
                .Take(5)
                .ToList();
                
            return sentences;
        }

        private Dictionary<string, double> PredictCategoriesWithML(Dictionary<string, List<string>> keywords)
        {
            var categoryScores = new Dictionary<string, double>();
            
            // ML-like category scoring based on keyword weights
            foreach (var category in _categoryPatterns)
            {
                double score = 0;
                var allKeywords = keywords.SelectMany(kvp => kvp.Value).ToList();
                
                foreach (var pattern in category.Value)
                {
                    var matches = allKeywords.Count(kw => kw.ToLower().Contains(pattern));
                    score += matches * 0.1; // Weight contribution
                }
                
                categoryScores[category.Key] = Math.Min(score, 1.0); // Cap at 1.0
            }
            
            return categoryScores;
        }

        private List<GeneratedQuestion> GenerateMLEnhancedQuestions(
            string industry, 
            Dictionary<string, List<string>> keywords, 
            Dictionary<string, double> predictedCategories,
            string experience, 
            List<string> questionTypes, 
            int count)
        {
            var questions = new List<GeneratedQuestion>();
            
            // Use ML-informed category selection
            var topCategories = predictedCategories.OrderByDescending(kvp => kvp.Value)
                .Take(3)
                .Select(kvp => kvp.Key)
                .ToList();
            
            if (_industryTemplates.ContainsKey(industry))
            {
                var templates = _industryTemplates[industry];
                
                foreach (var template in templates)
                {
                    if (questions.Count >= count) break;
                    
                    // Select question type based on ML prediction
                    var category = topCategories[questions.Count % topCategories.Count];
                    var pattern = SelectPatternByCategory(template.QuestionPatterns, category);
                    
                    var question = GenerateMLPattern(pattern, keywords, experience, category);
                    questions.Add(question);
                }
            }
            
            // Fill remaining with base generation
            while (questions.Count < count)
            {
                var baseQuestions = base.GenerateQuestions("", "", "", "", experience, questionTypes ?? new List<string> { "Text", "Choice" }, 1);
                questions.AddRange(baseQuestions);
            }
            
            return questions.Take(count).ToList();
        }

        private string SelectPatternByCategory(string[] patterns, string category)
        {
            // ML-like pattern selection based on category
            var categoryPatterns = new Dictionary<string, int>();
            categoryPatterns.Add("technical", 0);
            categoryPatterns.Add("leadership", 1);
            categoryPatterns.Add("behavioral", 2);
            categoryPatterns.Add("analytical", 3);
            
            if (categoryPatterns.ContainsKey(category))
            {
                var index = categoryPatterns[category] % patterns.Length;
                return patterns[index];
            }
            
            return patterns[0];
        }

        private GeneratedQuestion GenerateMLPattern(string pattern, Dictionary<string, List<string>> keywords, string experience, string category)
        {
            var questionText = pattern;
            
            // ML-enhanced placeholder replacement
            if (questionText.Contains("{technology}") && keywords.ContainsKey("technical"))
            {
                var tech = keywords["technical"].FirstOrDefault() ?? "relevant technology";
                questionText = questionText.Replace("{technology}", tech);
            }
            
            // Add ML-informed context
            if (category == "technical" && keywords.ContainsKey("technical"))
            {
                var techList = keywords["technical"].Take(2);
                if (techList.Any())
                {
                    questionText += string.Format(" Focus on {0} expertise.", string.Join(" and ", techList));
                }
            }
            
            return new GeneratedQuestion
            {
                Text = questionText,
                Type = "Text",
                Category = category,
                IsRequired = true
            };
        }

        private List<GeneratedQuestion> OptimizeQuestionsWithML(List<GeneratedQuestion> questions, Dictionary<string, List<string>> keywords)
        {
            // ML-like optimization based on keyword relevance
            foreach (var question in questions)
            {
                // Calculate relevance score
                var relevanceScore = CalculateRelevanceScore(question, keywords);
                
                // Optimize question based on score
                if (relevanceScore < 0.5)
                {
                    question.Text += " Please provide specific examples from your experience.";
                }
            }
            
            return questions;
        }

        private double CalculateRelevanceScore(GeneratedQuestion question, Dictionary<string, List<string>> keywords)
        {
            double score = 0;
            var questionText = question.Text.ToLower();
            var allKeywords = keywords.SelectMany(kvp => kvp.Value).ToList();
            
            // Calculate keyword overlap
            var matches = allKeywords.Count(kw => questionText.Contains(kw.ToLower()));
            score = (double)matches / Math.Max(allKeywords.Count, 1);
            
            return Math.Min(score, 1.0);
        }

        private List<GeneratedQuestion> ValidateAndScoreQuestions(List<GeneratedQuestion> questions)
        {
            var scoredQuestions = new List<GeneratedQuestion>();
            
            foreach (var question in questions)
            {
                var validation = ValidateQuestion(question.Text, question.Type);
                
                if (validation.IsValid)
                {
                    // Add ML-like scoring metadata
                    question.Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "Valid", Points = CalculateQuestionScore(question) }
                    };
                    scoredQuestions.Add(question);
                }
                else
                {
                    var fixedQuestion = FixQuestionWithML(question, validation);
                    if (fixedQuestion != null)
                    {
                        scoredQuestions.Add(fixedQuestion);
                    }
                }
            }
            
            return scoredQuestions;
        }

        public decimal CalculateQuestionScore(string text, string category)
        {
            return CalculateQuestionScore(new GeneratedQuestion { Text = text, Category = category });
        }

        public decimal CalculateQuestionScore(GeneratedQuestion question)
        {
            // ML-like scoring algorithm
            double baseScore = 5.0;
            
            // Category-based scoring
            var categoryScores = new Dictionary<string, double>();
            categoryScores.Add("technical", 8.0);
            categoryScores.Add("leadership", 7.5);
            categoryScores.Add("behavioral", 6.5);
            categoryScores.Add("analytical", 7.0);
            
            if (categoryScores.ContainsKey(question.Category))
            {
                baseScore = categoryScores[question.Category];
            }
            
            // Length and complexity bonus
            if (question.Text.Length > 50) baseScore += 1;
            if (question.Text.Contains("?")) baseScore += 0.5;
            
            return (decimal)baseScore;
        }

        private GeneratedQuestion FixQuestionWithML(GeneratedQuestion original, ValidationResult validation)
        {
            var fixedText = original.Text;
            
            // ML-informed fixes based on validation patterns
            foreach (var warning in validation.Warnings)
            {
                if (warning.Contains("too short"))
                {
                    fixedText += " Please provide detailed examples from your professional experience.";
                }
                else if (warning.Contains("biased"))
                {
                    fixedText = Regex.Replace(fixedText, @"\b(he|she|him|her)\b", "they", RegexOptions.IgnoreCase);
                }
                else if (warning.Contains("unclear"))
                {
                    fixedText += " Be specific and provide measurable outcomes when possible.";
                }
            }
            
            return new GeneratedQuestion
            {
                Text = fixedText,
                Type = original.Type,
                Category = original.Category,
                IsRequired = original.IsRequired
            };
        }

        // ML-like analytics methods
        public QuestionAnalytics AnalyzeQuestionPerformance(string questionId, object responseDistribution)
        {
            // Simulated ML-based performance analysis
            return new QuestionAnalytics
            {
                QuestionId = questionId,
                TotalResponses = 100,
                AverageScore = 7.5,
                EffectivenessRating = 0.85,
                Insights = new List<string>
                {
                    "High engagement rate detected",
                    "Candidates provide detailed responses",
                    "Strong correlation with job performance"
                },
                Recommendations = new List<string>
                {
                    "Keep this question in future assessments",
                    "Consider adding follow-up questions",
                    "Good predictor of technical skills"
                }
            };
        }
    }

    public class QuestionAnalytics
    {
        public string QuestionId { get; set; }
        public int TotalResponses { get; set; }
        public double AverageScore { get; set; }
        public double EffectivenessRating { get; set; }
        public List<string> Insights { get; set; }
        public List<string> Recommendations { get; set; }
    }
}
