using System.Collections.Generic;
using HR.Web.Models;

namespace HR.Web.Services
{
    public partial class QuestionnaireService
    {
        private QuestionTemplate GetSeniorDeveloperTemplate()
        {
            return new QuestionTemplate
            {
                Name = "Senior Developer Questionnaire",
                Description = "Comprehensive questionnaire for senior developer positions",
                Questions = BuildSeniorDeveloperQuestions(),
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
                Questions = BuildJuniorDeveloperQuestions(),
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
                Questions = BuildTeamLeadQuestions(),
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
                Questions = BuildProjectManagerQuestions(),
                ScoringWeights = new Dictionary<string, decimal>
                {
                    { "management", 0.4m },
                    { "communication", 0.3m },
                    { "planning", 0.2m },
                    { "technical", 0.1m }
                }
            };
        }

        private static List<GeneratedQuestion> BuildSeniorDeveloperQuestions()
        {
            return new List<GeneratedQuestion>
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
            };
        }

        private static List<GeneratedQuestion> BuildJuniorDeveloperQuestions()
        {
            return new List<GeneratedQuestion>
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
            };
        }

        private static List<GeneratedQuestion> BuildTeamLeadQuestions()
        {
            return new List<GeneratedQuestion>
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
            };
        }

        private static List<GeneratedQuestion> BuildProjectManagerQuestions()
        {
            return new List<GeneratedQuestion>
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
            };
        }
    }
}
