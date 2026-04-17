using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HR.Web.Services
{
    public class DynamicQuestionService
    {
        public class GeneratedQuestion
        {
            public GeneratedQuestion()
            {
                Id = Guid.NewGuid().ToString();
                Options = new List<QuestionOption>();
            }

            public string Id { get; set; }
            public string Text { get; set; }
            public string Type { get; set; } // Text, Choice, Number, Rating
            public string Category { get; set; }
            public List<QuestionOption> Options { get; set; }
            public int Difficulty { get; set; } // 1-5
            public int MaxPoints { get; set; }
        }

        public class QuestionOption
        {
            public string Text { get; set; }
            public int Points { get; set; }
        }

        public class QuestionGenerationResult
        {
            public QuestionGenerationResult()
            {
                Questions = new List<GeneratedQuestion>();
                Metadata = new Dictionary<string, object>();
            }

            public bool Success { get; set; }
            public List<GeneratedQuestion> Questions { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
            public string Message { get; set; }
        }

        public class JobAnalysis
        {
            public JobAnalysis()
            {
                TechnicalSkills = new List<string>();
                SoftSkills = new List<string>();
                Responsibilities = new List<string>();
                Qualifications = new List<string>();
                KeyTerms = new List<string>();
            }

            public string JobTitle { get; set; }
            public string SeniorityLevel { get; set; }
            public List<string> TechnicalSkills { get; set; }
            public List<string> SoftSkills { get; set; }
            public List<string> Responsibilities { get; set; }
            public List<string> Qualifications { get; set; }
            public string Industry { get; set; }
            public List<string> KeyTerms { get; set; }
        }

        public QuestionGenerationResult GenerateQuestions(
            string jobTitle,
            string jobDescription,
            string keyResponsibilities,
            string requiredQualifications)
        {
            return GenerateQuestions(jobTitle, jobDescription, keyResponsibilities, requiredQualifications, "mid", 5, null);
        }

        public QuestionGenerationResult GenerateQuestions(
            string jobTitle,
            string jobDescription,
            string keyResponsibilities,
            string requiredQualifications,
            string experience,
            int questionCount,
            List<string> questionTypes)
        {
            try
            {
                // Analyze the job information
                var analysis = AnalyzeJob(jobTitle, jobDescription, keyResponsibilities, requiredQualifications, experience);
                
                // Validate question count
                if (questionCount <= 0 || questionCount > 50)
                {
                    return new QuestionGenerationResult
                    {
                        Success = false,
                        Message = "Question count must be between 1 and 50"
                    };
                }

                // Default question types if not specified
                questionTypes = questionTypes ?? new List<string> { "Text", "Choice", "Number", "Rating" };

                var questions = new List<GeneratedQuestion>();
                var questionDistribution = DistributeQuestionTypes(questionCount, questionTypes);

                // Generate questions for each type
                foreach (var kvp in questionDistribution)
                {
                    var typeQuestions = GenerateQuestionsByType(kvp.Key, kvp.Value, analysis);
                    questions.AddRange(typeQuestions);
                }

                // Shuffle and limit to requested count
                questions = ShuffleSecurely(questions).Take(questionCount).ToList();

                return new QuestionGenerationResult
                {
                    Success = true,
                    Questions = questions,
                    Metadata = new Dictionary<string, object>
                    {
                        { "jobTitle", jobTitle },
                        { "experience", experience },
                        { "questionCount", questions.Count },
                        { "generatedAt", DateTime.UtcNow.ToString("O") },
                        { "analysis", analysis },
                        { "distribution", questionDistribution }
                    },
                    Message = string.Format("Successfully generated {0} dynamic questions", questions.Count)
                };
            }
            catch (Exception ex)
            {
                return new QuestionGenerationResult
                {
                    Success = false,
                    Message = "Error generating questions: " + ex.Message
                };
            }
        }

        private JobAnalysis AnalyzeJob(string jobTitle, string jobDescription, string keyResponsibilities, string requiredQualifications, string experience)
        {
            var analysis = new JobAnalysis
            {
                JobTitle = jobTitle != null ? jobTitle.Trim() : "",
                SeniorityLevel = DetermineSeniority(experience, jobTitle, jobDescription)
            };

            // Combine all text for analysis
            var allText = string.Format("{0} {1} {2} {3}", jobTitle, jobDescription, keyResponsibilities, requiredQualifications).ToLower();

            // Extract technical skills
            analysis.TechnicalSkills = ExtractTechnicalSkills(allText);

            // Extract soft skills
            analysis.SoftSkills = ExtractSoftSkills(allText);

            // Extract responsibilities
            analysis.Responsibilities = ExtractResponsibilities(keyResponsibilities, jobDescription);

            // Extract qualifications
            analysis.Qualifications = ExtractQualifications(requiredQualifications, jobDescription);

            // Determine industry
            analysis.Industry = DetermineIndustry(allText);

            // Extract key terms
            analysis.KeyTerms = ExtractKeyTerms(allText);

            return analysis;
        }

        private Dictionary<string, int> DistributeQuestionTypes(int totalQuestions, List<string> types)
        {
            var distribution = new Dictionary<string, int>();
            var availableTypes = types.Where(t => IsValidQuestionType(t)).ToList();
            
            if (!availableTypes.Any())
            {
                availableTypes = new List<string> { "Text", "Choice", "Number", "Rating" };
            }

            var baseCount = totalQuestions / availableTypes.Count;
            var remainder = totalQuestions % availableTypes.Count;

            for (int i = 0; i < availableTypes.Count; i++)
            {
                var count = baseCount + (i < remainder ? 1 : 0);
                distribution[availableTypes[i]] = count;
            }

            return distribution;
        }

        private List<GeneratedQuestion> GenerateQuestionsByType(string type, int count, JobAnalysis analysis)
        {
            var questions = new List<GeneratedQuestion>();

            switch (type.ToLower())
            {
                case "text":
                    questions.AddRange(GenerateTextQuestions(count, analysis));
                    break;
                case "choice":
                    questions.AddRange(GenerateChoiceQuestions(count, analysis));
                    break;
                case "number":
                    questions.AddRange(GenerateNumberQuestions(count, analysis));
                    break;
                case "rating":
                    questions.AddRange(GenerateRatingQuestions(count, analysis));
                    break;
            }

            return questions;
        }

        private List<GeneratedQuestion> GenerateTextQuestions(int count, JobAnalysis analysis)
        {
            var questions = new List<GeneratedQuestion>();
            
            for (int i = 0; i < count; i++)
            {
                var question = CreateDynamicTextQuestion(analysis, i);
                questions.Add(question);
            }

            return questions;
        }

        private GeneratedQuestion CreateDynamicTextQuestion(JobAnalysis analysis, int index)
        {
            var questionTypes = new Func<GeneratedQuestion>[]
            {
                // Experience questions
                () => new GeneratedQuestion
                {
                    Text = string.Format("Describe your experience with {0} and provide specific examples of projects where you've applied this skill.", GetRandomSkill(analysis)),
                    Category = "Technical Experience",
                    Difficulty = 3,
                    MaxPoints = 10
                },
                () => new GeneratedQuestion
                {
                    Text = string.Format("Tell us about the most challenging {0} project you've worked on and what made it successful.", analysis.JobTitle),
                    Category = "Problem Solving",
                    Difficulty = 4,
                    MaxPoints = 10
                },
                
                // Behavioral questions
                () => new GeneratedQuestion
                {
                    Text = string.Format("Describe a situation where you had to {0}. What was your approach and what was the outcome?", GetRandomResponsibility(analysis)),
                    Category = "Behavioral",
                    Difficulty = 3,
                    MaxPoints = 9
                },
                () => new GeneratedQuestion
                {
                    Text = string.Format("How do you handle disagreements with team members when working on {0} responsibilities?", analysis.JobTitle),
                    Category = "Teamwork",
                    Difficulty = 3,
                    MaxPoints = 8
                },
                
                // Motivation questions
                () => new GeneratedQuestion
                {
                    Text = string.Format("What specifically about this {0} position interests you and how does it align with your career goals?", analysis.JobTitle),
                    Category = "Motivation",
                    Difficulty = 2,
                    MaxPoints = 8
                },
                () => new GeneratedQuestion
                {
                    Text = string.Format("How do you stay current with developments in {0}?", analysis.Industry ?? "your field"),
                    Category = "Learning",
                    Difficulty = 3,
                    MaxPoints = 9
                },
                
                // Problem-solving questions
                () => new GeneratedQuestion
                {
                    Text = string.Format("Describe a time you had to learn a new {0} quickly. What was your learning process?", GetRandomSkill(analysis)),
                    Category = "Adaptability",
                    Difficulty = 3,
                    MaxPoints = 9
                },
                () => new GeneratedQuestion
                {
                    Text = string.Format("What strategies do you use to ensure quality when {0}?", GetRandomResponsibility(analysis)),
                    Category = "Quality Focus",
                    Difficulty = 3,
                    MaxPoints = 8
                }
            };

            var question = questionTypes[index % questionTypes.Length]();
            question.Type = "Text";
            question.Text = ReplaceTemplateVariables(question.Text, analysis);
            
            return question;
        }

        private List<GeneratedQuestion> GenerateChoiceQuestions(int count, JobAnalysis analysis)
        {
            var questions = new List<GeneratedQuestion>();
            
            for (int i = 0; i < count; i++)
            {
                var question = CreateDynamicChoiceQuestion(analysis, i);
                questions.Add(question);
            }

            return questions;
        }

        private GeneratedQuestion CreateDynamicChoiceQuestion(JobAnalysis analysis, int index)
        {
            var questionTypes = new Func<GeneratedQuestion>[]
            {
                // Communication preferences
                () => new GeneratedQuestion
                {
                    Text = "How do you prefer to communicate project updates to your team?",
                    Category = "Communication",
                    Difficulty = 2,
                    MaxPoints = 8,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "Daily written reports", Points = 3 },
                        new QuestionOption { Text = "Weekly team meetings", Points = 5 },
                        new QuestionOption { Text = "Real-time chat updates", Points = 4 },
                        new QuestionOption { Text = "As needed via email", Points = 2 }
                    }
                },
                
                // Work environment
                () => new GeneratedQuestion
                {
                    Text = "What work environment helps you perform your best as a " + analysis.JobTitle + "?",
                    Category = "Work Style",
                    Difficulty = 2,
                    MaxPoints = 8,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "Quiet individual workspace", Points = 3 },
                        new QuestionOption { Text = "Collaborative open office", Points = 4 },
                        new QuestionOption { Text = "Hybrid remote/in-office", Points = 5 },
                        new QuestionOption { Text = "Fully remote", Points = 4 }
                    }
                },
                
                // Technical proficiency
                () => new GeneratedQuestion
                {
                    Text = string.Format("How would you rate your proficiency with {0}?", GetRandomSkill(analysis)),
                    Category = "Technical Skills",
                    Difficulty = 3,
                    MaxPoints = 10,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "Expert - Can teach others", Points = 5 },
                        new QuestionOption { Text = "Advanced - Can work independently", Points = 4 },
                        new QuestionOption { Text = "Intermediate - Need some guidance", Points = 3 },
                        new QuestionOption { Text = "Beginner - Just learning", Points = 1 }
                    }
                },
                
                // Time management
                () => new GeneratedQuestion
                {
                    Text = "How do you approach managing multiple competing deadlines?",
                    Category = "Time Management",
                    Difficulty = 3,
                    MaxPoints = 9,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "Prioritize by impact and urgency", Points = 5 },
                        new QuestionOption { Text = "Work on one project at a time", Points = 3 },
                        new QuestionOption { Text = "Delegate when possible", Points = 4 },
                        new QuestionOption { Text = "Ask manager to prioritize", Points = 2 }
                    }
                },
                
                // Learning approach
                () => new GeneratedQuestion
                {
                    Text = "How do you prefer to learn new technologies or skills?",
                    Category = "Learning",
                    Difficulty = 2,
                    MaxPoints = 8,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "Hands-on experimentation", Points = 5 },
                        new QuestionOption { Text = "Structured courses/training", Points = 4 },
                        new QuestionOption { Text = "Mentorship/guidance", Points = 4 },
                        new QuestionOption { Text = "Reading documentation", Points = 3 }
                    }
                },
                
                // Team collaboration
                () => new GeneratedQuestion
                {
                    Text = "What role do you typically take in team projects?",
                    Category = "Teamwork",
                    Difficulty = 2,
                    MaxPoints = 8,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "Team leader/organizer", Points = 5 },
                        new QuestionOption { Text = "Subject matter expert", Points = 4 },
                        new QuestionOption { Text = "Support team player", Points = 3 },
                        new QuestionOption { Text = "Independent contributor", Points = 3 }
                    }
                }
            };

            var question = questionTypes[index % questionTypes.Length]();
            question.Type = "Choice";
            question.Text = ReplaceTemplateVariables(question.Text, analysis);
            
            return question;
        }

        private string GetVariationText(string baseText, int variationIndex, JobAnalysis analysis)
        {
            if (variationIndex == 0) return baseText;
            
            var variations = new List<string>();
            
            // Create variations based on the base text
            if (baseText.Contains("feedback"))
            {
                variations.Add("How do you prefer to give feedback to your colleagues?");
                variations.Add("What type of feedback motivates you most to improve?");
                variations.Add("How do you handle constructive criticism in the workplace?");
            }
            else if (baseText.Contains("work environment"))
            {
                variations.Add("What type of team structure helps you perform at your best?");
                variations.Add("How do you maintain focus in a busy work environment?");
                variations.Add("What tools or resources help you be most productive?");
            }
            else if (baseText.Contains("proficiency"))
            {
                // Use different technical skills for variations
                if (analysis.TechnicalSkills.Count > variationIndex)
                {
                    return string.Format("How would you rate your proficiency in {0}?", analysis.TechnicalSkills[variationIndex]);
                }
                variations.Add("How comfortable are you learning new technical skills?");
                variations.Add("How do you stay updated with technical developments in your field?");
            }
            else if (baseText.Contains("conflicting priorities"))
            {
                variations.Add("How do you approach managing multiple projects simultaneously?");
                variations.Add("What strategies do you use to meet tight deadlines?");
                variations.Add("How do you prioritize tasks when everything seems urgent?");
            }
            else
            {
                // Generic variations
                variations.Add(string.Format("In your experience, how would you approach {0}?", baseText.ToLower()));
                variations.Add(string.Format("What strategies have worked well for you regarding {0}?", baseText.ToLower()));
                variations.Add(string.Format("How would you improve your approach to {0}?", baseText.ToLower()));
            }
            
            return variations[Math.Min(variationIndex - 1, variations.Count - 1)];
        }

        private List<GeneratedQuestion> GenerateNumberQuestions(int count, JobAnalysis analysis)
        {
            var questions = new List<GeneratedQuestion>();
            
            for (int i = 0; i < count; i++)
            {
                var question = CreateDynamicNumberQuestion(analysis, i);
                questions.Add(question);
            }

            return questions;
        }

        private GeneratedQuestion CreateDynamicNumberQuestion(JobAnalysis analysis, int index)
        {
            var questionTypes = new Func<GeneratedQuestion>[]
            {
                // Experience years
                () => new GeneratedQuestion
                {
                    Text = "How many years of professional experience do you have in " + (analysis.Industry ?? "your field") + "?",
                    Category = "Experience",
                    Difficulty = 1,
                    MaxPoints = 8,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "0-2 years", Points = 2 },
                        new QuestionOption { Text = "3-5 years", Points = 4 },
                        new QuestionOption { Text = "6-10 years", Points = 6 },
                        new QuestionOption { Text = "10+ years", Points = 8 }
                    }
                },
                
                // Team projects
                () => new GeneratedQuestion
                {
                    Text = "How many team projects have you led or significantly contributed to in the past year?",
                    Category = "Teamwork",
                    Difficulty = 2,
                    MaxPoints = 8,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "None", Points = 1 },
                        new QuestionOption { Text = "1-2 projects", Points = 3 },
                        new QuestionOption { Text = "3-5 projects", Points = 5 },
                        new QuestionOption { Text = "6+ projects", Points = 8 }
                    }
                },
                
                // Technical skills rating
                () => new GeneratedQuestion
                {
                    Text = string.Format("On a scale of 1-10, how would you rate your proficiency with {0}?", GetRandomSkill(analysis)),
                    Category = "Technical Skills",
                    Difficulty = 2,
                    MaxPoints = 10,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "1-3 (Beginner)", Points = 2 },
                        new QuestionOption { Text = "4-6 (Intermediate)", Points = 5 },
                        new QuestionOption { Text = "7-8 (Advanced)", Points = 8 },
                        new QuestionOption { Text = "9-10 (Expert)", Points = 10 }
                    }
                },
                
                // Team size experience
                () => new GeneratedQuestion
                {
                    Text = "What's the largest team size you've worked with as a " + analysis.JobTitle + "?",
                    Category = "Team Experience",
                    Difficulty = 2,
                    MaxPoints = 8,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "1-3 people", Points = 2 },
                        new QuestionOption { Text = "4-8 people", Points = 4 },
                        new QuestionOption { Text = "9-15 people", Points = 6 },
                        new QuestionOption { Text = "16+ people", Points = 8 }
                    }
                },
                
                // Projects managed
                () => new GeneratedQuestion
                {
                    Text = "How many projects have you managed from start to finish?",
                    Category = "Project Management",
                    Difficulty = 3,
                    MaxPoints = 9,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "None", Points = 1 },
                        new QuestionOption { Text = "1-3 projects", Points = 4 },
                        new QuestionOption { Text = "4-10 projects", Points = 7 },
                        new QuestionOption { Text = "11+ projects", Points = 9 }
                    }
                }
            };

            var question = questionTypes[index % questionTypes.Length]();
            question.Type = "Number";
            question.Text = ReplaceTemplateVariables(question.Text, analysis);
            
            return question;
        }

        private List<GeneratedQuestion> GenerateRatingQuestions(int count, JobAnalysis analysis)
        {
            var questions = new List<GeneratedQuestion>();
            
            for (int i = 0; i < count; i++)
            {
                var question = CreateDynamicRatingQuestion(analysis, i);
                questions.Add(question);
            }

            return questions;
        }

        private GeneratedQuestion CreateDynamicRatingQuestion(JobAnalysis analysis, int index)
        {
            var questionTypes = new Func<GeneratedQuestion>[]
            {
                // Teamwork rating
                () => new GeneratedQuestion
                {
                    Text = "Rate your ability to work effectively in a team environment.",
                    Category = "Teamwork",
                    Difficulty = 2,
                    MaxPoints = 10,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "1", Points = 1 },
                        new QuestionOption { Text = "2", Points = 2 },
                        new QuestionOption { Text = "3", Points = 3 },
                        new QuestionOption { Text = "4", Points = 4 },
                        new QuestionOption { Text = "5", Points = 5 }
                    }
                },
                
                // Communication rating
                () => new GeneratedQuestion
                {
                    Text = "Rate your written and verbal communication skills.",
                    Category = "Communication",
                    Difficulty = 2,
                    MaxPoints = 10,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "1", Points = 1 },
                        new QuestionOption { Text = "2", Points = 2 },
                        new QuestionOption { Text = "3", Points = 3 },
                        new QuestionOption { Text = "4", Points = 4 },
                        new QuestionOption { Text = "5", Points = 5 }
                    }
                },
                
                // Adaptability rating
                () => new GeneratedQuestion
                {
                    Text = "Rate your ability to adapt to new challenges and changes.",
                    Category = "Adaptability",
                    Difficulty = 2,
                    MaxPoints = 10,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "1", Points = 1 },
                        new QuestionOption { Text = "2", Points = 2 },
                        new QuestionOption { Text = "3", Points = 3 },
                        new QuestionOption { Text = "4", Points = 4 },
                        new QuestionOption { Text = "5", Points = 5 }
                    }
                },
                
                // Technical skills rating
                () => new GeneratedQuestion
                {
                    Text = string.Format("Rate your proficiency with {0} and related technologies.", GetRandomSkill(analysis)),
                    Category = "Technical Skills",
                    Difficulty = 3,
                    MaxPoints = 10,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "1", Points = 1 },
                        new QuestionOption { Text = "2", Points = 2 },
                        new QuestionOption { Text = "3", Points = 3 },
                        new QuestionOption { Text = "4", Points = 4 },
                        new QuestionOption { Text = "5", Points = 5 }
                    }
                },
                
                // Problem-solving rating
                () => new GeneratedQuestion
                {
                    Text = "Rate your problem-solving abilities in high-pressure situations.",
                    Category = "Problem Solving",
                    Difficulty = 3,
                    MaxPoints = 10,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "1", Points = 1 },
                        new QuestionOption { Text = "2", Points = 2 },
                        new QuestionOption { Text = "3", Points = 3 },
                        new QuestionOption { Text = "4", Points = 4 },
                        new QuestionOption { Text = "5", Points = 5 }
                    }
                },
                
                // Leadership rating
                () => new GeneratedQuestion
                {
                    Text = "Rate your leadership and mentoring capabilities.",
                    Category = "Leadership",
                    Difficulty = 3,
                    MaxPoints = 10,
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Text = "1", Points = 1 },
                        new QuestionOption { Text = "2", Points = 2 },
                        new QuestionOption { Text = "3", Points = 3 },
                        new QuestionOption { Text = "4", Points = 4 },
                        new QuestionOption { Text = "5", Points = 5 }
                    }
                }
            };

            var question = questionTypes[index % questionTypes.Length]();
            question.Type = "Rating";
            question.Text = ReplaceTemplateVariables(question.Text, analysis);
            
            return question;
        }

        private string GetRandomSkill(JobAnalysis analysis)
        {
            if (analysis.TechnicalSkills == null || !analysis.TechnicalSkills.Any())
            {
                var defaultSkills = new[] { "problem-solving", "communication", "project management", "teamwork", "leadership" };
                return defaultSkills[GetSecureRandomInt(defaultSkills.Length)];
            }
            return analysis.TechnicalSkills[GetSecureRandomInt(analysis.TechnicalSkills.Count)];
        }

        private string GetRandomResponsibility(JobAnalysis analysis)
        {
            if (analysis.Responsibilities == null || !analysis.Responsibilities.Any())
            {
                var defaultResponsibilities = new[] { "managing projects", "working with clients", "developing solutions", "analyzing data", "leading teams" };
                return defaultResponsibilities[GetSecureRandomInt(defaultResponsibilities.Length)];
            }

            // Pick a responsibility and try to clean/shorten it
            var raw = analysis.Responsibilities[GetSecureRandomInt(analysis.Responsibilities.Count)];
            
            // If it's too long, try to take just the first part (before a comma or period)
            if (raw.Length > 80)
            {
                var separators = new[] { ',', '.', ';', '(', ')' };
                var firstPart = raw.Split(separators, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (firstPart != null && firstPart.Length > 10)
                {
                    return firstPart.Trim();
                }
            }
            
            return raw.Trim();
        }

        private static List<T> ShuffleSecurely<T>(IEnumerable<T> source)
        {
            var items = source.ToList();
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = GetSecureRandomInt(i + 1);
                var temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }

            return items;
        }

        private static int GetSecureRandomInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException("maxExclusive");
            }

            var bytes = new byte[4];
            var bound = (uint)maxExclusive;
            var max = uint.MaxValue - (uint.MaxValue % bound);
            uint value;

            using (var rng = RandomNumberGenerator.Create())
            {
                do
                {
                    rng.GetBytes(bytes);
                    value = BitConverter.ToUInt32(bytes, 0);
                } while (value >= max);
            }

            return (int)(value % bound);
        }

                // Helper methods for template variable replacement
        private string ReplaceTemplateVariables(string text, JobAnalysis analysis)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            return text
                .Replace("{jobTitle}", analysis.JobTitle ?? "position")
                .Replace("{industry}", analysis.Industry ?? "industry")
                .Replace("{seniority}", analysis.SeniorityLevel ?? "level");
        }

        private bool IsValidQuestionType(string type)
        {
            var validTypes = new[] { "text", "choice", "number", "rating" };
            return validTypes.Contains(type != null ? type.ToLower() : null);
        }

                private string DetermineSeniority(string experience, string jobTitle, string jobDescription)
        {
            var allText = string.Format("{0} {1} {2}", experience, jobTitle, jobDescription).ToLower();

            if (allText.Contains("senior") || allText.Contains("lead") || allText.Contains("principal") || allText.Contains("architect"))
                return "Senior";
            if (allText.Contains("junior") || allText.Contains("entry") || allText.Contains("intern"))
                return "Junior";
            if (allText.Contains("mid") || allText.Contains("intermediate"))
                return "Mid-level";

            var expLower = experience != null ? experience.ToLower() : "mid";
            switch (expLower)
            {
                case "senior":
                    return "Senior";
                case "junior":
                    return "Junior";
                case "lead":
                    return "Senior";
                case "entry":
                    return "Junior";
                default:
                    return "Mid-level";
            }
        }

        private List<string> ExtractTechnicalSkills(string text)
        {
            var techKeywords = new[]
            {
                "javascript", "python", "java", "c#", "c++", "php", "ruby", "go", "rust", "swift",
                "react", "angular", "vue", "node", "express", "django", "flask", "spring", "asp.net",
                "sql", "mysql", "postgresql", "mongodb", "redis", "elasticsearch",
                "aws", "azure", "gcp", "docker", "kubernetes", "terraform", "jenkins",
                "git", "github", "gitlab", "jira", "confluence",
                "html", "css", "sass", "webpack", "babel",
                "machine learning", "ai", "data science", "analytics", "big data",
                "mobile", "ios", "android", "react native", "flutter",
                "testing", "cypress", "jest", "selenium", "unit testing"
            };

            // Fix: Use Word Boundaries (\b) to avoid false positives like "ai" in "campaigns"
            var skills = new List<string>();
            foreach (var keyword in techKeywords)
            {
                // For keywords with space, we match literals, for short ones like 'ai', we use boundaries
                if (keyword.Length <= 3 || !keyword.Contains(" "))
                {
                    if (Regex.IsMatch(text, @"\b" + Regex.Escape(keyword) + @"\b", RegexOptions.IgnoreCase))
                    {
                        skills.Add(keyword);
                    }
                }
                else if (text.Contains(keyword))
                {
                    skills.Add(keyword);
                }
            }
            return skills;
        }

        private List<string> ExtractSoftSkills(string text)
        {
            var softSkillKeywords = new[]
            {
                "communication", "leadership", "teamwork", "collaboration", "problem solving",
                "critical thinking", "creativity", "innovation", "adaptability", "flexibility",
                "time management", "organization", "planning", "project management", "mentoring",
                "coaching", "presentation", "negotiation", "interpersonal", "emotional intelligence"
            };

            return softSkillKeywords.Where(keyword => text.Contains(keyword)).ToList();
        }

        private List<string> ExtractResponsibilities(string keyResponsibilities, string jobDescription)
        {
            var text = (keyResponsibilities ?? "") + " " + (jobDescription ?? "");
            // split by bullet points, newlines, and sentence markers
            var lines = text.Split(new[] { '\n', '\r', '•', '*', '►', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var responsibilities = new List<string>();
            
            foreach (var line in lines)
            {
                var clean = line.Trim();
                // Filter out short filler words or meta-text
                if (clean.Length > 15 && clean.Length < 150)
                {
                    responsibilities.Add(clean);
                }
                else if (clean.Length >= 150)
                {
                    // If it's a long paragraph, break it into sentences
                    var sentences = Regex.Split(clean, @"(?<=[.!?])\s+").Where(s => s.Length > 15 && s.Length < 150);
                    responsibilities.AddRange(sentences);
                }
            }
            
            return responsibilities.Distinct().ToList();
        }

        private List<string> ExtractQualifications(string requiredQualifications, string jobDescription)
        {
            var text = requiredQualifications + " " + jobDescription;
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+").Where(s => s.Length > 10).ToList();
            return sentences.Take(5).Select(s => s.Trim().TrimEnd('.')).ToList();
        }

        private string DetermineIndustry(string text)
        {
            if (text.Contains("software") || text.Contains("developer") || text.Contains("programming"))
                return "Technology";
            if (text.Contains("marketing") || text.Contains("sales") || text.Contains("advertising"))
                return "Marketing/Sales";
            if (text.Contains("finance") || text.Contains("accounting") || text.Contains("banking"))
                return "Finance";
            if (text.Contains("healthcare") || text.Contains("medical") || text.Contains("nursing"))
                return "Healthcare";
            if (text.Contains("education") || text.Contains("teaching") || text.Contains("academic"))
                return "Education";
            if (text.Contains("hr") || text.Contains("human resources") || text.Contains("recruitment"))
                return "Recruitment";

            return "General";
        }

        private List<string> ExtractKeyTerms(string text)
        {
            var words = Regex.Split(text, @"\W+").Where(w => w.Length > 3).ToList();
            var commonWords = new[] { "with", "have", "will", "from", "they", "been", "said", "each", "which", "their", "time", "will" };
            return words.Where(w => !commonWords.Contains(w.ToLower())).Distinct().Take(10).ToList();
        }

        private class QuestionTemplate
        {
            public string Text { get; set; }
            public string Category { get; set; }
            public int Difficulty { get; set; }
            public int MaxPoints { get; set; }
        }
    }
}
