using System;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class ApplicationsController
    {
        private const string LinkedInImportStateSessionKey = "__Applications_LinkedInImport_State";
        private const string LinkedInImportPositionIdSessionKey = "__Applications_LinkedInImport_PositionId";
        private const string LinkedInImportedProfileSessionKey = "__Applications_LinkedInImport_Profile";

        [Authorize]
        public ActionResult StartLinkedInImport(int positionId)
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

            var tenantValidationResult = ValidatePositionTenantAccess(position, "Access Denied: Position belongs to another company.");
            if (tenantValidationResult != null)
            {
                return tenantValidationResult;
            }

            var applicant = FindOrCreateApplicantForPosition(position.CompanyId);
            if (applicant == null)
            {
                TempData["ErrorMessage"] = "Please complete your applicant profile before continuing.";
                return RedirectToAction("Index", "Positions");
            }

            var linkedInService = new LinkedInProfileImportService();
            if (!linkedInService.IsConfigured())
            {
                TempData["ErrorMessage"] = "LinkedIn import is not available yet. An administrator needs to configure it first.";
                return RedirectToAction("ProfileDetails", new
                {
                    tenant = RouteData.Values["tenant"] as string,
                    positionId = positionId
                });
            }

            var state = Guid.NewGuid().ToString("N");
            Session[LinkedInImportStateSessionKey] = state;
            Session[LinkedInImportPositionIdSessionKey] = positionId;

            var tenantToken = RouteData.Values["tenant"] as string;
            var redirectUri = BuildLinkedInImportCallbackUri(tenantToken);
            var authorizationUrl = linkedInService.BuildAuthorizationUrl(redirectUri, state);

            return Redirect(authorizationUrl);
        }

        [Authorize]
        public ActionResult LinkedInImportCallback(string code, string state, string error, string error_description)
        {
            var tenantToken = RouteData.Values["tenant"] as string;
            var positionId = GetLinkedInImportPositionId();
            if (!positionId.HasValue)
            {
                return RedirectToAction("Index", "Positions", new { tenant = tenantToken });
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                ClearLinkedInImportStateToken();
                TempData["ErrorMessage"] = BuildLinkedInImportErrorMessage(error, error_description);
                return RedirectToAction("ProfileDetails", new { tenant = tenantToken, positionId = positionId.Value });
            }

            var expectedState = Session[LinkedInImportStateSessionKey] as string;
            ClearLinkedInImportStateToken();
            if (string.IsNullOrWhiteSpace(expectedState) || !string.Equals(expectedState, state, StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] = "We could not verify the LinkedIn import request. Please try again.";
                return RedirectToAction("ProfileDetails", new { tenant = tenantToken, positionId = positionId.Value });
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["ErrorMessage"] = "LinkedIn did not return an authorization code.";
                return RedirectToAction("ProfileDetails", new { tenant = tenantToken, positionId = positionId.Value });
            }

            try
            {
                var linkedInService = new LinkedInProfileImportService();
                var redirectUri = BuildLinkedInImportCallbackUri(tenantToken);
                var importedProfile = linkedInService.ImportProfile(code, redirectUri);
                Session[LinkedInImportedProfileSessionKey] = importedProfile;

                TempData["SuccessMessage"] = importedProfile != null && importedProfile.HasData
                    ? "LinkedIn basic profile imported. Review the fields below and complete the remaining details."
                    : "LinkedIn authentication succeeded, but there were no free profile fields available to import.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LinkedIn import failed: " + ex);
                TempData["ErrorMessage"] = "LinkedIn import failed. " + ex.Message;
            }

            return RedirectToAction("ProfileDetails", new { tenant = tenantToken, positionId = positionId.Value });
        }

        private void PopulateLinkedInImportViewBag()
        {
            var linkedInService = new LinkedInProfileImportService();
            ViewBag.LinkedInImportAvailable = linkedInService.IsConfigured();
            ViewBag.LinkedInImportUnavailableReason = linkedInService.GetUnavailabilityReason();
            ViewBag.LinkedInImportHelpText = "Imports the free LinkedIn fields available for profile prefill. Experience history, education, skills, and availability still need manual review.";
            ViewBag.LinkedInImportedProfile = Session[LinkedInImportedProfileSessionKey] as LinkedInBasicProfileResult;
        }

        private void ApplyPendingLinkedInImport(ApplicantProfileViewModel model)
        {
            if (model == null)
            {
                return;
            }

            var importedProfile = Session[LinkedInImportedProfileSessionKey] as LinkedInBasicProfileResult;
            if (importedProfile == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(importedProfile.FullName))
            {
                model.FullName = importedProfile.FullName;
            }

            if (string.IsNullOrWhiteSpace(model.PortfolioUrl) && !string.IsNullOrWhiteSpace(importedProfile.ProfileUrl))
            {
                model.PortfolioUrl = importedProfile.ProfileUrl;
            }
        }

        private void ClearPendingLinkedInImport()
        {
            Session.Remove(LinkedInImportedProfileSessionKey);
            Session.Remove(LinkedInImportPositionIdSessionKey);
            Session.Remove(LinkedInImportStateSessionKey);
        }

        private void ClearLinkedInImportStateToken()
        {
            Session.Remove(LinkedInImportStateSessionKey);
        }

        private int? GetLinkedInImportPositionId()
        {
            if (Session[LinkedInImportPositionIdSessionKey] is int positionId)
            {
                return positionId;
            }

            var rawValue = Session[LinkedInImportPositionIdSessionKey] as string;
            if (!string.IsNullOrWhiteSpace(rawValue) && int.TryParse(rawValue, out positionId))
            {
                return positionId;
            }

            return null;
        }

        private string BuildLinkedInImportCallbackUri(string tenantToken)
        {
            var callbackPath = Url.Action("LinkedInImportCallback", "Applications", new { tenant = tenantToken });
            var baseUrl = ExternalUrlHelper.GetBaseUrl(Request);
            var baseUri = new Uri(EnsureTrailingSlash(baseUrl), UriKind.Absolute);
            return new Uri(baseUri, callbackPath.TrimStart('/')).ToString();
        }

        private static string EnsureTrailingSlash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "http://localhost/";
            }

            return value.EndsWith("/", StringComparison.Ordinal)
                ? value
                : value + "/";
        }

        private static string BuildLinkedInImportErrorMessage(string error, string errorDescription)
        {
            if (string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase))
            {
                return "LinkedIn import was cancelled before access was granted.";
            }

            if (!string.IsNullOrWhiteSpace(errorDescription))
            {
                return "LinkedIn import could not be completed: " + errorDescription;
            }

            return "LinkedIn import could not be completed.";
        }
    }
}
