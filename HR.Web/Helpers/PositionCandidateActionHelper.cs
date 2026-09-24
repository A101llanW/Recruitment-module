using System;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Helpers
{
    public static class PositionCandidateActionHelper
    {
        public static PositionCandidateActionViewModel Resolve(
            bool isAuthenticated,
            Application existingApplication,
            Position position,
            System.Web.Mvc.UrlHelper url,
            bool hasScheduledInterview = false)
        {
            if (position == null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            var positionId = position.Id;
            var tenantSlug = position.Company != null && !string.IsNullOrWhiteSpace(position.Company.Slug)
                ? position.Company.Slug.Trim()
                : null;
            var coverLetterUrl = url.Action("CoverLetter", "Applications", new { tenant = tenantSlug, positionId = positionId });

            if (!isAuthenticated)
            {
                return new PositionCandidateActionViewModel
                {
                    Label = "Sign In to Apply",
                    Url = url.Action("Login", "Account", new { returnUrl = coverLetterUrl }),
                    IconClass = "fas fa-paper-plane"
                };
            }

            if (existingApplication == null)
            {
                return new PositionCandidateActionViewModel
                {
                    Label = "Apply",
                    Url = coverLetterUrl,
                    IconClass = "fas fa-paper-plane"
                };
            }

            PositionCandidateActionViewModel action;
            if (existingApplication.PendingQuestionnaireStage.HasValue)
            {
                action = new PositionCandidateActionViewModel
                {
                    Label = "Open Questionnaire",
                    Url = url.Action("Questionnaire", "Applications", new { tenant = tenantSlug, positionId = positionId }),
                    IconClass = "fas fa-clipboard-list"
                };
            }
            else
            {
                action = new PositionCandidateActionViewModel
                {
                    Label = "My Applications",
                    Url = url.Action("Index", "Applications", new { tenant = tenantSlug }),
                    IconClass = "fas fa-file-alt"
                };
            }

            ApplyApplicationBadge(existingApplication, position, hasScheduledInterview, action);
            return action;
        }

        private static void ApplyApplicationBadge(
            Application application,
            Position position,
            bool hasScheduledInterview,
            PositionCandidateActionViewModel action)
        {
            if (application == null || action == null)
            {
                return;
            }

            action.HasApplied = true;
            action.AppliedOn = application.AppliedOn;

            if (application.PendingQuestionnaireStage.HasValue)
            {
                action.BadgeLabel = "Questionnaire due";
                action.BadgeCssClass = "position-applied-badge--action";
                return;
            }

            var status = (application.Status ?? string.Empty).Trim().ToLowerInvariant();
            switch (status)
            {
                case "hired":
                    action.BadgeLabel = "Hired";
                    action.BadgeCssClass = "position-applied-badge--hired";
                    return;
                case "offer":
                    action.BadgeLabel = "Offer received";
                    action.BadgeCssClass = "position-applied-badge--offer";
                    return;
                case "rejected":
                    action.BadgeLabel = "Not selected";
                    action.BadgeCssClass = "position-applied-badge--closed";
                    return;
            }

            if (hasScheduledInterview)
            {
                action.BadgeLabel = "Interview scheduled";
                action.BadgeCssClass = "position-applied-badge--review";
                return;
            }

            switch (status)
            {
                case "interviewing":
                    action.BadgeLabel = "In review";
                    action.BadgeCssClass = "position-applied-badge--review";
                    return;
                case "shortlisted":
                    action.BadgeLabel = "Shortlisted";
                    action.BadgeCssClass = "position-applied-badge--offer";
                    return;
                case "approved":
                    action.BadgeLabel = "Approved";
                    action.BadgeCssClass = "position-applied-badge--review";
                    return;
                case "pending":
                    action.BadgeLabel = "Application received";
                    action.BadgeCssClass = "position-applied-badge--applied";
                    return;
            }

            var maxStages = position != null ? Math.Max(1, position.QuestionnaireStageCount) : 1;
            var completedStages = Math.Max(0, application.LastCompletedQuestionnaireStage);
            if (maxStages > 1 && completedStages > 0 && completedStages < maxStages)
            {
                action.BadgeLabel = "Awaiting next stage";
                action.BadgeCssClass = "position-applied-badge--review";
                return;
            }

            if (completedStages >= maxStages && completedStages > 0)
            {
                action.BadgeLabel = "Under review";
                action.BadgeCssClass = "position-applied-badge--review";
                return;
            }

            action.BadgeLabel = "Applied";
            action.BadgeCssClass = "position-applied-badge--applied";
        }
    }
}
