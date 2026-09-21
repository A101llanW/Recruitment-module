using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using HR.Web.Data;

namespace HR.Web.Helpers
{
    public static class SecurityLogTranslator
    {
        private const string VisitorUsername = "Visitor";
        private const string LegacyGuestUsername = "Guest";

        public static bool IsVisitorActivity(string username)
        {
            return string.Equals(username, VisitorUsername, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(username, LegacyGuestUsername, StringComparison.OrdinalIgnoreCase);
        }

        public static string DescribeVisitorActivity(
            string companyName,
            string controller,
            string action,
            RouteValueDictionary routeValues,
            HttpRequestBase request)
        {
            var controllerName = NormalizeToken(controller);
            var actionName = NormalizeToken(action);
            var positionTitle = ResolvePositionTitle(routeValues, request);

            switch (controllerName)
            {
                case "Positions":
                    if (actionName == "Index")
                    {
                        return "Browsed open job listings";
                    }

                    if (actionName == "Details" && !string.IsNullOrWhiteSpace(positionTitle))
                    {
                        return string.Format("Viewed the {0} job opening", positionTitle);
                    }

                    if (actionName == "Details")
                    {
                        return "Viewed a job opening";
                    }

                    break;

                case "Account":
                    if (actionName == "Login")
                    {
                        return "Opened the sign-in page";
                    }

                    if (actionName == "Register")
                    {
                        return "Opened the registration page";
                    }

                    if (actionName == "ForgotPassword")
                    {
                        return "Opened the forgot-password page";
                    }

                    if (actionName == "ResetPassword")
                    {
                        return "Opened the reset-password page";
                    }

                    break;

                case "Applications":
                    if (!string.IsNullOrWhiteSpace(positionTitle) &&
                        (actionName == "CoverLetter" || actionName == "Questionnaire" || actionName == "Create"))
                    {
                        return string.Format("Started applying for the {0} position", positionTitle);
                    }

                    if (actionName == "Questionnaire")
                    {
                        return "Opened an application questionnaire";
                    }

                    if (actionName == "CoverLetter")
                    {
                        return "Started a job application";
                    }

                    break;

                case "Home":
                    if (actionName == "Privacy")
                    {
                        return "Read the privacy policy";
                    }

                    if (actionName == "Terms")
                    {
                        return "Read the terms and conditions";
                    }

                    break;

                case "Interviews":
                    if (actionName == "Index")
                    {
                        return "Opened the interviews page";
                    }

                    break;

                case "Departments":
                    if (actionName == "Index")
                    {
                        return "Browsed departments";
                    }

                    break;
            }

            var pageLabel = DescribePageLabel(controllerName, actionName);
            if (!string.IsNullOrWhiteSpace(companyName))
            {
                return string.Format("Viewed {0} on the {1} careers site", pageLabel, companyName);
            }

            return string.Format("Viewed {0} on the careers site", pageLabel);
        }

        public static string DescribeAuditActivity(
            string username,
            string actionCode,
            string controller,
            string entityId,
            bool wasSuccessful,
            string storedSummary,
            string technicalError)
        {
            if (!string.IsNullOrWhiteSpace(storedSummary) && LooksLikeFriendlySummary(storedSummary))
            {
                return storedSummary;
            }

            if (!wasSuccessful && !string.IsNullOrWhiteSpace(technicalError))
            {
                return string.Format(
                    "{0} ({1})",
                    DescribeAuditActivityCore(username, actionCode, controller, entityId, wasSuccessful),
                    technicalError);
            }

            return DescribeAuditActivityCore(username, actionCode, controller, entityId, wasSuccessful);
        }

        public static string DescribeLoginAttempt(string username, bool wasSuccessful, string storedSummary)
        {
            if (IsVisitorActivity(username))
            {
                return NormalizeLegacyVisitorSummary(storedSummary);
            }

            var displayName = FormatPersonName(username);

            if (wasSuccessful)
            {
                return string.Format("{0} signed in successfully", displayName);
            }

            return DescribeLoginFailure(storedSummary, displayName);
        }

        public static string DescribeLoginFailure(string technicalReason, string username = null)
        {
            var person = string.IsNullOrWhiteSpace(username) ? "Someone" : FormatPersonName(username);
            var reason = (technicalReason ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(reason))
            {
                return string.Format("{0} could not sign in", person);
            }

            if (reason.StartsWith("Branded portal visit:", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeLegacyVisitorSummary(reason);
            }

            switch (reason.ToLowerInvariant())
            {
                case "invalid password":
                    return string.Format("{0} could not sign in: incorrect password", person);
                case "account locked":
                    return string.Format("{0} could not sign in: account locked after too many failed attempts", person);
                case "identifier not found":
                case "identifier not found in tenant":
                    return string.Format("{0} could not sign in: account not found", person);
                case "global admin used tenant portal":
                    return string.Format("{0} could not sign in from this company sign-in page", person);
                default:
                    if (reason.StartsWith("FORGOT_PASSWORD", StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Format("{0} requested a password reset link", person);
                    }

                    return string.Format("{0} could not sign in: {1}", person, reason);
            }
        }

        private static string DescribeAuditActivityCore(
            string username,
            string actionCode,
            string controller,
            string entityId,
            bool wasSuccessful)
        {
            var actor = IsVisitorActivity(username) || string.Equals(username, "Anonymous", StringComparison.OrdinalIgnoreCase)
                ? "A visitor"
                : string.Format("{0}", FormatPersonName(username));
            var controllerName = NormalizeToken(controller);
            var actionName = ExtractActionName(actionCode);
            var verb = ExtractVerb(actionCode);

            if (string.Equals(controllerName, "Positions", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actionName, "Index", StringComparison.OrdinalIgnoreCase))
            {
                return actor + " browsed job listings";
            }

            if (string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actionName, "Login", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(verb, "POST", StringComparison.OrdinalIgnoreCase))
            {
                return actor + " tried to sign in";
            }

            if (string.Equals(actionCode, "PORTAL_VISIT", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeLegacyVisitorSummary(storedSummaryFromAudit(entityId));
            }

            if (string.Equals(verb, "VIEW", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("{0} viewed {1}", actor, DescribePageLabel(controllerName, actionName));
            }

            if (string.Equals(verb, "POST", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("{0} submitted {1}", actor, DescribePageLabel(controllerName, actionName));
            }

            if (!wasSuccessful)
            {
                return string.Format("{0} had a problem with {1}", actor, DescribePageLabel(controllerName, actionName));
            }

            return string.Format("{0} used {1}", actor, DescribePageLabel(controllerName, actionName));
        }

        private static string storedSummaryFromAudit(string entityId)
        {
            return entityId;
        }

        private static string NormalizeLegacyVisitorSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return "Visited the careers site";
            }

            if (!summary.StartsWith("Branded portal visit:", StringComparison.OrdinalIgnoreCase))
            {
                return summary.Trim();
            }

            var path = summary.Substring("Branded portal visit:".Length).Trim();
            return DescribeLegacyPath(path);
        }

        private static string DescribeLegacyPath(string path)
        {
            var normalized = (path ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return "Visited the careers site";
            }

            var segments = normalized
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            if (segments.Count >= 2 &&
                string.Equals(segments[0], segments[1], StringComparison.OrdinalIgnoreCase))
            {
                segments.RemoveAt(0);
            }

            if (segments.Count == 0)
            {
                return "Visited the careers site";
            }

            var controller = segments.Count > 1 ? segments[segments.Count - 2] : segments[0];
            var action = segments[segments.Count - 1];
            var route = new RouteValueDictionary();
            return DescribeVisitorActivity(null, controller, action, route, null);
        }

        private static string ResolvePositionTitle(RouteValueDictionary routeValues, HttpRequestBase request)
        {
            int? positionId = ExtractPositionId(routeValues, request);
            if (!positionId.HasValue)
            {
                return null;
            }

            try
            {
                using (var uow = new UnitOfWork())
                {
                    var position = uow.Positions.Get(positionId.Value);
                    return position != null ? position.Title : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int? ExtractPositionId(RouteValueDictionary routeValues, HttpRequestBase request)
        {
            if (routeValues != null)
            {
                object routeId;
                if (routeValues.TryGetValue("id", out routeId))
                {
                    int parsedRouteId;
                    if (routeId != null && int.TryParse(routeId.ToString(), out parsedRouteId))
                    {
                        return parsedRouteId;
                    }
                }

                object positionIdValue;
                if (routeValues.TryGetValue("positionId", out positionIdValue))
                {
                    int parsedPositionId;
                    if (positionIdValue != null && int.TryParse(positionIdValue.ToString(), out parsedPositionId))
                    {
                        return parsedPositionId;
                    }
                }
            }

            if (request == null)
            {
                return null;
            }

            int parsedQueryPositionId;
            if (int.TryParse(request.QueryString["positionId"], out parsedQueryPositionId))
            {
                return parsedQueryPositionId;
            }

            return null;
        }

        private static string DescribePageLabel(string controller, string action)
        {
            var controllerLabel = DescribeController(controller);
            var actionLabel = DescribeAction(action);
            if (string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase))
            {
                return controllerLabel;
            }

            return string.Format("{0} ({1})", controllerLabel, actionLabel);
        }

        private static string DescribeController(string controller)
        {
            switch (NormalizeToken(controller))
            {
                case "Positions": return "job listings";
                case "Applications": return "applications";
                case "Account": return "account pages";
                case "Interviews": return "interviews";
                case "Departments": return "departments";
                case "Admin": return "admin settings";
                case "Home": return "site information";
                case "Applicants": return "applicants";
                case "Reports":
                case "ReportGenerator": return "reports";
                default: return HumanizeToken(controller);
            }
        }

        private static string DescribeAction(string action)
        {
            switch (NormalizeToken(action))
            {
                case "Index": return "list";
                case "Details": return "details";
                case "Create": return "create form";
                case "Edit": return "edit form";
                case "Delete": return "delete";
                case "Login": return "sign in";
                case "Register": return "registration";
                case "CoverLetter": return "cover letter";
                case "Questionnaire": return "questionnaire";
                default: return HumanizeToken(action);
            }
        }

        private static string ExtractActionName(string actionCode)
        {
            if (string.IsNullOrWhiteSpace(actionCode))
            {
                return string.Empty;
            }

            var parts = actionCode.Split(new[] { ':' }, 2);
            return parts.Length == 2 ? parts[1] : actionCode;
        }

        private static string ExtractVerb(string actionCode)
        {
            if (string.IsNullOrWhiteSpace(actionCode))
            {
                return string.Empty;
            }

            var parts = actionCode.Split(new[] { ':' }, 2);
            return parts.Length == 2 ? parts[0] : string.Empty;
        }

        private static bool LooksLikeFriendlySummary(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            return !trimmed.StartsWith("VIEW:", StringComparison.OrdinalIgnoreCase) &&
                   !trimmed.StartsWith("POST:", StringComparison.OrdinalIgnoreCase) &&
                   !trimmed.StartsWith("Branded portal visit:", StringComparison.OrdinalIgnoreCase) &&
                   !trimmed.StartsWith("PORTAL_VISIT", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatPersonName(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return "Someone";
            }

            if (IsVisitorActivity(username) || string.Equals(username, "Anonymous", StringComparison.OrdinalIgnoreCase))
            {
                return "A visitor";
            }

            return username.Trim();
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string HumanizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "a page";
            }

            var chars = new List<char> { char.ToUpper(value[0]) };
            for (var i = 1; i < value.Length; i++)
            {
                var current = value[i];
                if (char.IsUpper(current) && char.IsLower(value[i - 1]))
                {
                    chars.Add(' ');
                }

                chars.Add(i == 0 ? char.ToUpper(current) : char.ToLower(current));
            }

            return new string(chars.ToArray());
        }
    }
}
