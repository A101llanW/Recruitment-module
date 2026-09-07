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
            var coverLetterUrl = url.Action("CoverLetter", "Applications", new { positionId = positionId });

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

            if (existingApplication.PendingQuestionnaireStage.HasValue)
            {
                return new PositionCandidateActionViewModel
                {
                    Label = "Open Questionnaire",
                    Url = url.Action("Questionnaire", "Applications", new { positionId = positionId }),
                    IconClass = "fas fa-clipboard-list"
                };
            }

            return new PositionCandidateActionViewModel
            {
                Label = "View Application",
                Url = url.Action("Details", "Applications", new { id = existingApplication.Id }),
                IconClass = "fas fa-file-alt"
            };
        }
    }
}