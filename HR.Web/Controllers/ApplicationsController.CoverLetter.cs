using System.Web.Mvc;
using HR.Web.Filters;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class ApplicationsController
    {
        private static readonly string CoverLetterSession = "CoverLetter";
        private static readonly string CoverLetterPositionSession = "CoverLetterPositionId";

        // Step 1 of the candidate application flow:
        // CoverLetter -> ProfileDetails -> Questionnaire
        public ActionResult CoverLetter(int positionId)
        {
            if (!IsCurrentUserAuthenticated())
            {
                return RedirectToApplicationRegistration();
            }

            var position = _uow.Positions.Get(positionId);
            if (position == null)
            {
                return HttpNotFound();
            }

            var closedPositionRedirect = GetClosedPositionRedirect(position);
            if (closedPositionRedirect != null)
            {
                return closedPositionRedirect;
            }

            var applicant = FindOrCreateApplicantForPosition(position.CompanyId);
            if (applicant == null)
            {
                TempData["ErrorMessage"] = "Please complete your applicant profile before continuing.";
                return RedirectToAction("Index", "Positions");
            }

            if (HasExistingApplication(applicant.Id, positionId))
            {
                TempData["ErrorMessage"] = "You have already applied for this position.";
                return RedirectToAction("Index", "Positions");
            }

            var model = new CoverLetterViewModel
            {
                PositionId = position.Id,
                PositionTitle = position.Title,
                CoverLetter = GetPendingCoverLetter(position.Id)
            };

            return View(model);
        }

        [HttpPost]
        [TenantAuthorize]
        [ValidateAntiForgeryToken]
        public ActionResult CoverLetter(CoverLetterViewModel model)
        {
            if (model == null || model.PositionId <= 0)
            {
                return RedirectToAction("Index", "Positions");
            }

            var position = _uow.Positions.Get(model.PositionId);
            if (position == null)
            {
                return HttpNotFound();
            }

            var closedPositionRedirect = GetClosedPositionRedirect(position);
            if (closedPositionRedirect != null)
            {
                return closedPositionRedirect;
            }

            var applicant = FindOrCreateApplicantForPosition(position.CompanyId);
            if (applicant == null)
            {
                TempData["ErrorMessage"] = "Please complete your applicant profile before continuing.";
                return RedirectToAction("Index", "Positions");
            }

            if (HasExistingApplication(applicant.Id, model.PositionId))
            {
                TempData["ErrorMessage"] = "You have already applied for this position.";
                return RedirectToAction("Index", "Positions");
            }

            model.PositionTitle = position.Title;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            SetPendingCoverLetter(model.PositionId, model.CoverLetter);

            return RedirectToAction("ProfileDetails", GetApplicationFlowRouteValues(position.Id));
        }

        private bool HasPendingCoverLetter(int positionId)
        {
            return !string.IsNullOrWhiteSpace(GetPendingCoverLetter(positionId));
        }

        private string GetPendingCoverLetter(int positionId)
        {
            var storedPositionId = GetCoverLetterPositionIdFromSession();
            if (!storedPositionId.HasValue || storedPositionId.Value != positionId)
            {
                return null;
            }

            return Session[CoverLetterSession] as string;
        }

        private void SetPendingCoverLetter(int positionId, string coverLetter)
        {
            Session[CoverLetterPositionSession] = positionId;
            Session[CoverLetterSession] = string.IsNullOrWhiteSpace(coverLetter) ? null : coverLetter.Trim();
        }

        private int? GetCoverLetterPositionIdFromSession()
        {
            var raw = Session[CoverLetterPositionSession];
            if (raw is int)
            {
                return (int)raw;
            }

            return null;
        }

        private ActionResult RedirectToCoverLetter(int positionId)
        {
            TempData["ErrorMessage"] = "Please write a cover letter for this position before continuing.";
            return RedirectToAction("CoverLetter", GetApplicationFlowRouteValues(positionId));
        }

        private void ClearPendingCoverLetter()
        {
            Session.Remove(CoverLetterSession);
            Session.Remove(CoverLetterPositionSession);
        }

        private ActionResult RequireCoverLetterForNewApplication(Applicant applicant, int positionId)
        {
            if (applicant == null)
            {
                return RedirectToAction("Index", "Positions");
            }

            if (HasExistingApplication(applicant.Id, positionId))
            {
                return null;
            }

            return HasPendingCoverLetter(positionId) ? null : RedirectToCoverLetter(positionId);
        }
    }
}
