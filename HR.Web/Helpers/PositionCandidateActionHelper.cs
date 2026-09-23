using System;
using System.Web.Mvc;
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
            System.Web.Mvc.UrlHelper url)
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
                    Label = "View Application",
                    Url = url.Action("Details", "Applications", new { id = existingApplication.Id }),
                    IconClass = "fas fa-file-alt"
                };
            }

            ApplyApplicationBadge(existingApplication, action);
            return action;
        }

        private static void ApplyApplicationBadge(Application application, PositionCandidateActionViewModel action)
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

            var status = (application.Status ?? string.Empty).Trim();
            switch (status.ToLowerInvariant())
            {
                case "hired":
                    action.BadgeLabel = "Hired";
                    action.BadgeCssClass = "position-applied-badge--hired";
                    break;
                case "offer":
                    action.BadgeLabel = "Offer received";
                    action.BadgeCssClass = "position-applied-badge--offer";
                    break;
                case "rejected":
                    action.BadgeLabel = "Not selected";
                    action.BadgeCssClass = "position-applied-badge--closed";
                    break;
                case "interviewing":
                    action.BadgeLabel = "In review";
                    action.BadgeCssClass = "position-applied-badge--review";
                    break;
                default:
                    action.BadgeLabel = "Applied";
                    action.BadgeCssClass = "position-applied-badge--applied";
                    break;
            }
        }
    }
}