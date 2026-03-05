using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HR.Web.Models;

namespace HR.Web.Services
{
    public class GeneratedQuestion
    {
        public GeneratedQuestion()
        {
            Options = new List<QuestionOption>();
            IsRequired = true;
        }

        public string Text { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public List<QuestionOption> Options { get; set; }
        public bool IsRequired { get; set; }
    }

    public class QuestionTemplate
    {
        public QuestionTemplate()
        {
            Questions = new List<GeneratedQuestion>();
            ScoringWeights = new Dictionary<string, decimal>();
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public List<GeneratedQuestion> Questions { get; set; }
        public Dictionary<string, decimal> ScoringWeights { get; set; }
    }

    public class ValidationResult
    {
        public ValidationResult()
        {
            Warnings = new List<string>();
            BiasedTerms = new List<string>();
            Suggestions = new List<string>();
        }

        public bool IsValid { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> BiasedTerms { get; set; }
        public List<string> Suggestions { get; set; }
        public string Category { get; set; }
    }

    public class QuestionnaireService
    {
        private readonly List<string> _biasedTerms = new List<string>
        {
            "age", "young", "old", "recent graduate", "years of age",
            "male", "female", "gender", "married", "single", "kids", "children",
            "race", "ethnicity", "nationality", "disability", "religion",
            "pregnant", "pregnancy", "family status", "sexual orientation"
        };

        private readonly List<string> _techKeywords = new List<string>
        {
            "javascript", "python", "java", "c#", "sql", "react", "angular", "node.js", 
            "aws", "azure", "docker", "kubernetes", "git", "agile", "scrum", "devops",
            "microservices", "api", "rest", "graphql", "mongodb", "postgresql", "mysql"
        };

        private readonly List<string> _skillKeywords = new List<string>
        {
            "experience", "skills", "knowledge", "expertise", "proficiency", "background",
            "leadership", "communication", "teamwork", "project", "management", "analytical"
        };

        public List<GeneratedQuestion> GenerateQuestions(string jobTitle, string jobDescription, 
            string keyResponsibilities = "", string requiredQualifications = "", 
            string experience = "mid", List<string> questionTypes = null, int count = 5)
        {
            if (count <= 0 || count > 25)
                throw new ArgumentException("Number of questions must be between 1 and 25");

            questionTypes = questionTypes ?? new List<string> { "Text", "Choice", "Number", "Rating" };
            var questions = new List<GeneratedQuestion>();
            var keywords = ExtractKeywords(jobDescription + " " + keyResponsibilities + " " + requiredQualifications);

            // Distribute questions among requested types
            var availableTypes = questionTypes.Where(t => new[] { "Text", "Choice", "Number", "Rating" }.Contains(t)).ToList();
            var baseQuestionsPerType = Math.Max(1, count / availableTypes.Count);
            var remainingQuestions = count % availableTypes.Count;

            int questionIndex = 0;

            // Generate Text questions
            if (questionTypes.Contains("Text"))
            {
                var textQuestions = GenerateTextQuestions(jobTitle, keywords, experience);
                var textCount = Math.Min(baseQuestionsPerType + (questionIndex < remainingQuestions ? 1 : 0), textQuestions.Count);
                questions.AddRange(textQuestions.Take(textCount));
                questionIndex++;
            }

            // Generate Choice questions
            if (questionTypes.Contains("Choice") && questions.Count < count)
            {
                var choiceQuestions = GenerateChoiceQuestions(jobTitle, keywords, experience);
                var choiceCount = Math.Min(baseQuestionsPerType + (questionIndex < remainingQuestions ? 1 : 0), choiceQuestions.Count);
                questions.AddRange(choiceQuestions.Take(choiceCount));
                questionIndex++;
            }

            // Generate Number questions
            if (questionTypes.Contains("Number") && questions.Count < count)
            {
                var numberQuestions = GenerateNumberQuestions(experience);
                var numberCount = Math.Min(baseQuestionsPerType + (questionIndex < remainingQuestions ? 1 : 0), numberQuestions.Count);
                questions.AddRange(numberQuestions.Take(numberCount));
                questionIndex++;
            }

            // Generate Rating questions
            if (questionTypes.Contains("Rating") && questions.Count < count)
            {
                var ratingQuestions = GenerateRatingQuestions(experience);
                var ratingCount = Math.Min(baseQuestionsPerType + (questionIndex < remainingQuestions ? 1 : 0), ratingQuestions.Count);
                questions.AddRange(ratingQuestions.Take(ratingCount));
                questionIndex++;
            }

            return questions.Take(count).ToList();
        }

        public ValidationResult ValidateQuestion(string question, string questionType = "Text")
        {
            var result = new ValidationResult { IsValid = true };
            var lowerQuestion = question.ToLower();

            // Check for biased terms
            foreach (var term in _biasedTerms)
            {
                if (lowerQuestion.Contains(term))
                {
                    result.BiasedTerms.Add(term);
                    result.Warnings.Add(string.Format("Potentially biased term detected: '{0}'", term));
                }
            }

            // Check for red flags
            var redFlags = new List<string>
            {
                "personal life", "home life", "family responsibilities",
                "physical requirements", "medical condition", "arrest record"
            };

            foreach (var flag in redFlags)
            {
                if (lowerQuestion.Contains(flag))
                {
                    result.Warnings.Add(string.Format("Potentially inappropriate content: '{0}'", flag));
                }
            }

            // Check question clarity
            if (question.Length < 10)
            {
                result.Warnings.Add("Question seems too short and may lack clarity");
            }

            if (question.Length > 500)
            {
                result.Warnings.Add("Question is very long and may confuse candidates");
            }

            // Check for double negatives
            if (lowerQuestion.Contains("not") && lowerQuestion.Count(c => c == ' ') > 10)
            {
                var notCount = Regex.Matches(lowerQuestion, @"\bnot\b").Count;
                if (notCount > 1)
                {
                    result.Warnings.Add("Question contains multiple negatives which may be confusing");
                }
            }

            // Generate suggestions
            if (result.BiasedTerms.Any() || result.Warnings.Any())
            {
                result.Suggestions.Add("Focus on skills and qualifications relevant to the job");
                result.Suggestions.Add("Use inclusive language that doesn't discriminate");
                result.Suggestions.Add("Ensure the question directly relates to job performance");
            }

            result.IsValid = !result.BiasedTerms.Any() && result.Warnings.Count <= 2;

            return result;
        }

        public List<QuestionOption> SuggestPoints(string question, List<string> options, string difficulty = "intermediate")
        {
            var questionOptions = new List<QuestionOption>();
            int basePoints;
            switch (difficulty.ToLower())
            {
                case "easy": basePoints = 2; break;
                case "intermediate": basePoints = 3; break;
                case "hard": basePoints = 4; break;
                default: basePoints = 3; break;
            }

            for (int i = 0; i < options.Count; i++)
            {
                var points = CalculatePointsForOption(options[i], i, options.Count, basePoints);
                questionOptions.Add(new QuestionOption
                {
                    Text = options[i],
                    Points = points
                });
            }

            return questionOptions.OrderBy(o => o.Points).ToList();
        }

        public QuestionTemplate GetTemplate(string templateType)
        {
            switch (templateType.ToLower())
            {
                case "senior-developer": return GetSeniorDeveloperTemplate();
                case "junior-developer": return GetJuniorDeveloperTemplate();
                case "team-lead": return GetTeamLeadTemplate();
                case "project-manager": return GetProjectManagerTemplate();
                default: throw new ArgumentException(string.Format("Unknown template type: {0}", templateType));
            }
        }

        private Dictionary<string, List<string>> ExtractKeywords(string text)
        {
            var keywords = new Dictionary<string, List<string>>();
            var words = Regex.Split(text.ToLower(), @"\W+")
                .Where(w => w.Length > 2)
                .ToList();

            keywords["technical"] = words.Where(w => _techKeywords.Contains(w)).ToList();
            keywords["skills"] = words.Where(w => _skillKeywords.Contains(w)).ToList();
            keywords["primary"] = new List<string> { keywords["technical"].FirstOrDefault() ?? "technology" };

            return keywords;
        }

        private List<GeneratedQuestion> GenerateTextQuestions(string jobTitle, Dictionary<string, List<string>> keywords, string experience)
        {
            var primaryTech = keywords.ContainsKey("primary") ? keywords["primary"].FirstOrDefault() : "relevant technologies";
            
            return new List<GeneratedQuestion>
            {
                new GeneratedQuestion
                {
                    Text = string.Format("Describe your experience with {0}.", primaryTech),
                    Type = "Text",
                    Category = "technical"
                },
                new GeneratedQuestion
                {
                    Text = "Describe a challenging situation you faced at work and how you resolved it.",
                    Type = "Text",
                    Category = "problem-solving"
                },
                new GeneratedQuestion
                {
                    Text = "How do you handle feedback and criticism in the workplace?",
                    Type = "Text",
                    Category = "professionalism"
                },
                new GeneratedQuestion
                {
                    Text = string.Format("What interests you about the {0} role?", jobTitle),
                    Type = "Text",
                    Category = "motivation"
                },
                new GeneratedQuestion
                {
                    Text = "Describe your approach to problem-solving when faced with unexpected challenges.",
                    Type = "Text",
                    Category = "analytical"
                }
            };
        }

        private List<GeneratedQuestion> GenerateChoiceQuestions(string jobTitle, Dictionary<string, List<string>> keywords, string experience)
        {
            return new List<GeneratedQuestion>
            {
                new GeneratedQuestion
                {
                    Text = "How do you prefer to receive feedback on your work?",
                    Type = "Choice",
                    Category = "professionalism",
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "I prefer not to receive feedback", Points = 1 },
                        new QuestionOption { Text = "Written feedback via email", Points = 4 },
                        new QuestionOption { Text = "One-on-one discussions", Points = 7 },
                        new QuestionOption { Text = "Regular, constructive feedback in any format", Points = 10 }
                    }
                },
                new GeneratedQuestion
                {
                    Text = "What type of work environment helps you be most productive?",
                    Type = "Choice",
                    Category = "work-style",
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "Quiet, isolated environment", Points = 3 },
                        new QuestionOption { Text = "Collaborative team space", Points = 6 },
                        new QuestionOption { Text = "Flexible hybrid arrangement", Points = 8 },
                        new QuestionOption { Text = "Adaptable to various environments", Points = 10 }
                    }
                },
                new GeneratedQuestion
                {
                    Text = "How do you approach learning new technologies or skills?",
                    Type = "Choice",
                    Category = "learning",
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "I wait for training to be provided", Points = 2 },
                        new QuestionOption { Text = "I learn when required for projects", Points = 5 },
                        new QuestionOption { Text = "I proactively explore new technologies", Points = 8 },
                        new QuestionOption { Text = "I continuously learn and share knowledge with others", Points = 10 }
                    }
                }
            };
        }

        private List<GeneratedQuestion> GenerateNumberQuestions(string experience)
        {
            return new List<GeneratedQuestion>
            {
                new GeneratedQuestion
                {
                    Text = "How many years of professional experience do you have?",
                    Type = "Number",
                    Category = "experience"
                },
                new GeneratedQuestion
                {
                    Text = "How many team projects have you collaborated on?",
                    Type = "Number",
                    Category = "teamwork"
                },
                new GeneratedQuestion
                {
                    Text = "How many technical skills or programming languages are you proficient in?",
                    Type = "Number",
                    Category = "technical"
                }
            };
        }

        private List<GeneratedQuestion> GenerateRatingQuestions(string experience)
        {
            return new List<GeneratedQuestion>
            {
                new GeneratedQuestion
                {
                    Text = "Rate your proficiency with problem-solving and analytical thinking.",
                    Type = "Rating",
                    Category = "analytical"
                },
                new GeneratedQuestion
                {
                    Text = "Rate your ability to work effectively in a team environment.",
                    Type = "Rating",
                    Category = "teamwork"
                },
                new GeneratedQuestion
                {
                    Text = "Rate your written and verbal communication skills.",
                    Type = "Rating",
                    Category = "communication"
                },
                new GeneratedQuestion
                {
                    Text = "Rate your ability to adapt to new challenges and changes.",
                    Type = "Rating",
                    Category = "adaptability"
                }
            };
        }

        private decimal CalculatePointsForOption(string option, int index, int totalCount, int basePoints)
        {
            // Distribute points based on position and quality indicators
            var multiplier = 1.0m;
            
            // Positive indicators
            var positiveWords = new[] { "excellent", "expert", "advanced", "lead", "senior", "proactive", "innovative" };
            if (positiveWords.Any(word => option.ToLower().Contains(word)))
                multiplier += 0.5m;

            // Negative indicators
            var negativeWords = new[] { "no", "none", "never", "avoid", "basic", "beginner" };
            if (negativeWords.Any(word => option.ToLower().Contains(word)))
                multiplier -= 0.3m;

            // Scale based on position (better options get more points)
            var positionMultiplier = (decimal)(index + 1) / totalCount;
            
            return Math.Round(basePoints * multiplier * positionMultiplier * 2, 0);
        }

        private QuestionTemplate GetSeniorDeveloperTemplate()
        {
            return new QuestionTemplate
            {
                Name = "Senior Developer Questionnaire",
                Description = "Comprehensive questionnaire for senior developer positions",
                Questions = new List<GeneratedQuestion>
                {
                    new GeneratedQuestion
                    {
                        Text = "How many years of professional development experience do you have?",
                        Type = "Number",
                        Category = "experience",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "Describe your experience with system architecture and design.",
                        Type = "Text",
                        Category = "architecture",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "How do you mentor junior developers?",
                        Type = "Text",
                        Category = "leadership",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "What's your experience with code reviews and quality assurance?",
                        Type = "Choice",
                        Category = "quality",
                        IsRequired = true,
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "No experience", Points = 0 },
                            new QuestionOption { Text = "Participate in reviews", Points = 5 },
                            new QuestionOption { Text = "Lead review process", Points = 8 },
                            new QuestionOption { Text = "Establish review standards and best practices", Points = 10 }
                        }
                    }
                },
                ScoringWeights = new Dictionary<string, decimal>
                {
                    { "technical", 0.4m },
                    { "behavioral", 0.3m },
                    { "leadership", 0.2m },
                    { "experience", 0.1m }
                }
            };
        }

        private QuestionTemplate GetJuniorDeveloperTemplate()
        {
            return new QuestionTemplate
            {
                Name = "Junior Developer Questionnaire",
                Description = "Entry-level questionnaire for junior developer positions",
                Questions = new List<GeneratedQuestion>
                {
                    new GeneratedQuestion
                    {
                        Text = "What programming languages are you proficient in?",
                        Type = "Text",
                        Category = "technical",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "Describe any personal or academic projects you've worked on.",
                        Type = "Text",
                        Category = "experience",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "How do you approach learning new technologies?",
                        Type = "Text",
                        Category = "learning",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "Are you comfortable working in a team environment?",
                        Type = "Choice",
                        Category = "teamwork",
                        IsRequired = true,
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Prefer working alone", Points = 2 },
                            new QuestionOption { Text = "Comfortable with teamwork", Points = 6 },
                            new QuestionOption { Text = "Thrive in collaborative environments", Points = 10 }
                        }
                    }
                },
                ScoringWeights = new Dictionary<string, decimal>
                {
                    { "technical", 0.5m },
                    { "potential", 0.3m },
                    { "teamwork", 0.2m }
                }
            };
        }

        private QuestionTemplate GetTeamLeadTemplate()
        {
            return new QuestionTemplate
            {
                Name = "Team Lead Questionnaire",
                Description = "Questionnaire for team leadership positions",
                Questions = new List<GeneratedQuestion>
                {
                    new GeneratedQuestion
                    {
                        Text = "Describe your experience leading development teams.",
                        Type = "Text",
                        Category = "leadership",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "How do you handle conflicts within your team?",
                        Type = "Text",
                        Category = "conflict-resolution",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "What's your approach to project planning and execution?",
                        Type = "Text",
                        Category = "project-management",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "How do you balance technical leadership with administrative responsibilities?",
                        Type = "Choice",
                        Category = "prioritization",
                        IsRequired = true,
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Focus mainly on technical work", Points = 3 },
                            new QuestionOption { Text = "Split time evenly", Points = 6 },
                            new QuestionOption { Text = "Prioritize team leadership", Points = 8 },
                            new QuestionOption { Text = "Adapt based on project needs", Points = 10 }
                        }
                    }
                },
                ScoringWeights = new Dictionary<string, decimal>
                {
                    { "leadership", 0.4m },
                    { "technical", 0.3m },
                    { "communication", 0.2m },
                    { "planning", 0.1m }
                }
            };
        }

        private QuestionTemplate GetProjectManagerTemplate()
        {
            return new QuestionTemplate
            {
                Name = "Project Manager Questionnaire",
                Description = "Questionnaire for project management positions",
                Questions = new List<GeneratedQuestion>
                {
                    new GeneratedQuestion
                    {
                        Text = "Describe your experience managing software projects.",
                        Type = "Text",
                        Category = "experience",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "How do you handle project delays and budget overruns?",
                        Type = "Text",
                        Category = "risk-management",
                        IsRequired = true
                    },
                    new GeneratedQuestion
                    {
                        Text = "What project management methodologies are you familiar with?",
                        Type = "Choice",
                        Category = "methodology",
                        IsRequired = true,
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Waterfall only", Points = 3 },
                            new QuestionOption { Text = "Agile/Scrum only", Points = 6 },
                            new QuestionOption { Text = "Multiple methodologies", Points = 8 },
                            new QuestionOption { Text = "Hybrid approaches based on project", Points = 10 }
                        }
                    },
                    new GeneratedQuestion
                    {
                        Text = "How do you communicate project status to stakeholders?",
                        Type = "Text",
                        Category = "communication",
                        IsRequired = true
                    }
                },
                ScoringWeights = new Dictionary<string, decimal>
                {
                    { "management", 0.4m },
                    { "communication", 0.3m },
                    { "planning", 0.2m },
                    { "technical", 0.1m }
                }
            };
        }
    }
}
