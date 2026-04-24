using System;
using System.Collections.Generic;
using System.Linq;

namespace HR.Web.Services
{
    public static class RoleAccessLevels
    {
        public const string None = "None";
        public const string View = "View";
        public const string Manage = "Manage";

        public static readonly string[] OrderedValues = { None, View, Manage };
    }

    public sealed class RoleModuleDefinition
    {
        public RoleModuleDefinition(string key, string displayName, string description, string iconClass)
        {
            Key = key;
            DisplayName = displayName;
            Description = description;
            IconClass = iconClass;
        }

        public string Key { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public string IconClass { get; private set; }
    }

    public static class RoleModuleCatalog
    {
        public const string Applications = "Applications";
        public const string Applicants = "Applicants";
        public const string Departments = "Departments";
        public const string Interviews = "Interviews";
        public const string Onboardings = "Onboardings";
        public const string Positions = "Positions";
        public const string Questions = "Questions";
        public const string Reports = "Reports";
        public const string SecurityLogs = "SecurityLogs";
        public const string UserManagement = "UserManagement";

        private static readonly HashSet<string> ManageActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AddGeneratedQuestionsToBank",
            "AddGeneratedQuestionsToSample",
            "AddQuestionsToSample",
            "BatchDeleteQuestions",
            "BookInterview",
            "BulkRecalculateScores",
            "Builder",
            "CheckDuplicateQuestions",
            "CheckQuestionStatus",
            "CloneQuestionnaire",
            "Create",
            "CreatePositionWithQuestions",
            "CreateUser",
            "Delete",
            "DeleteConfirmed",
            "DeleteQuestion",
            "Edit",
            "GenerateQuestions",
            "GetDepartments",
            "LivePreview",
            "PositionQuestions",
            "ProcessDuplicateDecisions",
            "PutOnHold",
            "RecalculateScores",
            "ReleaseHold",
            "SaveBuilder",
            "SavePositionQuestions",
            "UnlockUserAccount",
            "UpdateApplicationScore",
            "UpdateStatus",
            "UpdateUserRole"
        };

        private static readonly IReadOnlyList<RoleModuleDefinition> Modules = new List<RoleModuleDefinition>
        {
            new RoleModuleDefinition(Positions, "Positions", "View or manage job openings and their publication lifecycle.", "fa-sitemap"),
            new RoleModuleDefinition(Applications, "Applications", "Review incoming job applications and candidate progression.", "fa-file-alt"),
            new RoleModuleDefinition(Interviews, "Interviews", "Coordinate interview schedules and interviewer assignments.", "fa-comments"),
            new RoleModuleDefinition(Questions, "Questions", "Maintain question banks, questionnaires, and scoring inputs.", "fa-question-circle"),
            new RoleModuleDefinition(Onboardings, "Onboardings", "Track and manage onboarding activities for hires.", "fa-user-check"),
            new RoleModuleDefinition(Applicants, "Applicants", "Browse applicant records, CVs, and profile activity.", "fa-users"),
            new RoleModuleDefinition(Departments, "Departments", "Manage department structures used by positions and reporting.", "fa-building"),
            new RoleModuleDefinition(UserManagement, "User Management", "Update company users, roles, and account access.", "fa-user-shield"),
            new RoleModuleDefinition(SecurityLogs, "Security Logs", "Review login activity, audit events, and account security history.", "fa-shield-alt"),
            new RoleModuleDefinition(Reports, "Reports & Analytics", "Access rankings, scoring insights, exports, and generated reports.", "fa-chart-bar")
        }.AsReadOnly();

        public static IReadOnlyList<RoleModuleDefinition> All
        {
            get { return Modules; }
        }

        public static RoleModuleDefinition Find(string moduleKey)
        {
            return Modules.FirstOrDefault(m => string.Equals(m.Key, moduleKey, StringComparison.OrdinalIgnoreCase));
        }

        public static string ResolveModule(string controllerName, string actionName)
        {
            controllerName = controllerName ?? string.Empty;
            actionName = actionName ?? string.Empty;

            if (string.Equals(controllerName, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                switch (actionName)
                {
                    case "Questions":
                    case "EditQuestion":
                    case "DeleteQuestion":
                    case "BatchDeleteQuestions":
                    case "ExportQuestionBank":
                    case "AddToSampleQuestions":
                    case "GenerateQuestions":
                    case "CheckQuestionStatus":
                    case "CheckDuplicateQuestions":
                    case "AddGeneratedQuestionsToSample":
                    case "AddQuestionsToSample":
                    case "ProcessDuplicateDecisions":
                    case "CreatePositionWithQuestions":
                    case "GetDepartments":
                    case "AddGeneratedQuestionsToBank":
                    case "PositionQuestions":
                    case "SavePositionQuestions":
                        return Questions;

                    case "CandidateRankings":
                    case "ViewApplicationDetails":
                    case "EnhancedCandidateRankings":
                    case "ApplicationScoreDetails":
                    case "ScoringStatistics":
                    case "RecalculateScores":
                    case "ExportRankings":
                    case "UpdateApplicationScore":
                    case "BulkRecalculateScores":
                        return Reports;

                    case "UserManagement":
                    case "CreateUser":
                    case "UpdateUserRole":
                    case "UnlockUserAccount":
                    case "GlobalUserManagement":
                        return UserManagement;

                    case "SecurityLogs":
                        return SecurityLogs;
                }

                return null;
            }

            if (string.Equals(controllerName, "Applicants", StringComparison.OrdinalIgnoreCase))
            {
                return Applicants;
            }

            if (string.Equals(controllerName, "Applications", StringComparison.OrdinalIgnoreCase))
            {
                return Applications;
            }

            if (string.Equals(controllerName, "Departments", StringComparison.OrdinalIgnoreCase))
            {
                return Departments;
            }

            if (string.Equals(controllerName, "Interviews", StringComparison.OrdinalIgnoreCase))
            {
                return Interviews;
            }

            if (string.Equals(controllerName, "Onboardings", StringComparison.OrdinalIgnoreCase))
            {
                return Onboardings;
            }

            if (string.Equals(controllerName, "Positions", StringComparison.OrdinalIgnoreCase))
            {
                return Positions;
            }

            if (string.Equals(controllerName, "Questionnaire", StringComparison.OrdinalIgnoreCase))
            {
                return Questions;
            }

            if (string.Equals(controllerName, "ReportGenerator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controllerName, "Reports", StringComparison.OrdinalIgnoreCase))
            {
                return Reports;
            }

            return null;
        }

        public static string ResolveRequiredAccessLevel(string httpMethod, string actionName)
        {
            if (!string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return RoleAccessLevels.Manage;
            }

            return ManageActions.Contains(actionName ?? string.Empty)
                ? RoleAccessLevels.Manage
                : RoleAccessLevels.View;
        }
    }
}
