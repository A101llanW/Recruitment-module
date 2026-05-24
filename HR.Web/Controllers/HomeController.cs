using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using HR.Web.Data;
using HR.Web.Helpers;
using HR.Web.Models;

namespace HR.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly UnitOfWork _legalDocUow = new UnitOfWork();

        public ActionResult Index()
        {
            return HttpNotFound();
        }

        public ActionResult Debug()
        {
            if (AppConfig.IsProduction)
            {
                return HttpNotFound();
            }

            return View();
        }

        public ActionResult About()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Privacy()
        {
            return BuildLegalDocumentView(true);
        }

        [AllowAnonymous]
        public ActionResult Terms()
        {
            return BuildLegalDocumentView(false);
        }

        private ActionResult BuildLegalDocumentView(bool isPrivacyPage)
        {
            var kind = ResolveLegalDocumentRelationship();
            ViewBag.HideNavbar = true;
            ViewBag.LegalDocTenant = RouteData.Values["tenant"] as string;
            ViewBag.LegalDocPage = isPrivacyPage ? "privacy" : "terms";
            ViewBag.CompanyName = LegalPolicyHelper.CompanyName;
            ViewBag.ContactEmail = LegalPolicyHelper.ContactEmail;
            ViewBag.ContactAddress = LegalPolicyHelper.ContactAddress;
            ViewBag.LastUpdated = "May 15, 2026";
            ViewBag.LegalRelationship = kind;
            if (isPrivacyPage)
            {
                ViewBag.PrivacyVersionLabel = LegalPolicyHelper.GetPrivacyVersion(kind);
                return View("Privacy");
            }

            ViewBag.TermsVersionLabel = LegalPolicyHelper.GetTermsVersion(kind);
            return View("Terms");
        }

        /// <summary>
        /// Resolves which policy text applies: authenticated user from account role; else pending-login user from session; else applicant (public).
        /// </summary>
        private LegalRelationshipKind ResolveLegalDocumentRelationship()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated && !string.IsNullOrWhiteSpace(User.Identity.Name))
            {
                var identityName = User.Identity.Name;
                // Match AccountController.ResolveCurrentUserFromIdentity: EF6 cannot translate string.Equals(..., StringComparison) to SQL.
                // Scope by company id from the forms ticket so the correct row is returned when usernames repeat across tenants.
                var principalUser = FindPrincipalUserForLegalDocuments(identityName);
                if (principalUser != null)
                {
                    return LegalPolicyHelper.ResolveUserLegalRelationship(principalUser);
                }
            }

            var pendingId = LegalConsentSession.TryReadUserId(Session);
            if (pendingId.HasValue && LegalConsentSession.IsFresh(Session))
            {
                var pendingUser = _legalDocUow.Users.Get(pendingId.Value);
                if (pendingUser != null)
                {
                    return LegalPolicyHelper.ResolveUserLegalRelationship(pendingUser);
                }
            }

            return LegalRelationshipKind.Applicant;
        }

        private static FormsAuthenticationTicket TryDecryptFormsTicket(HttpCookie cookie)
        {
            if (cookie == null || string.IsNullOrEmpty(cookie.Value))
            {
                return null;
            }

            try
            {
                return FormsAuthentication.Decrypt(cookie.Value);
            }
            catch
            {
                return null;
            }
        }

        private int? TryReadCompanyIdFromFormsUserData()
        {
            var ticket = TryDecryptFormsTicket(Request.Cookies[FormsAuthentication.FormsCookieName]);
            if (ticket == null || string.IsNullOrEmpty(ticket.UserData))
            {
                return null;
            }

            var props = ticket.UserData.Split('|');
            if (props.Length < 2)
            {
                return null;
            }

            return int.TryParse(props[1], out var companyId) ? companyId : (int?)null;
        }

        /// <summary>
        /// Resolves the Users row for the signed-in principal, mirroring AccountController.ResolveCurrentUserFromIdentity (forms UserData + username).
        /// </summary>
        private User FindPrincipalUserForLegalDocuments(string identityName)
        {
            if (string.IsNullOrWhiteSpace(identityName))
            {
                return null;
            }

            var lowerUsername = identityName.ToLower();
            var companyId = TryReadCompanyIdFromFormsUserData();
            var dbUsers = _legalDocUow.Context.Users;

            if (companyId.HasValue)
            {
                var scoped = dbUsers.FirstOrDefault(u => u.UserName != null && u.UserName.ToLower() == lowerUsername && u.CompanyId == companyId.Value);
                if (scoped != null)
                {
                    return scoped;
                }
            }
            else
            {
                var platformScoped = dbUsers.FirstOrDefault(u => u.UserName != null && u.UserName.ToLower() == lowerUsername && u.CompanyId == null);
                if (platformScoped != null)
                {
                    return platformScoped;
                }
            }

            return dbUsers.FirstOrDefault(u => u.UserName != null && u.UserName.ToLower() == lowerUsername);
        }

        /// <summary>
        /// Serve company logo image
        /// </summary>
        [AllowAnonymous]
        public ActionResult CompanyLogo()
        {
            try
            {
                // Try the new transparent logo first
                string logoPath = Server.MapPath("~/Content/images/nanosoft-logo-transparent.png");
                if (System.IO.File.Exists(logoPath))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(logoPath);
                    return File(fileBytes, "image/png");
                }
                
                // Fallback to the JPG version
                logoPath = Server.MapPath("~/Content/images/nanosoft-logo.jpg");
                if (System.IO.File.Exists(logoPath))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(logoPath);
                    return File(fileBytes, "image/jpeg");
                }
                
                // Fallback to the original logo
                logoPath = Server.MapPath("~/Content/images/company-logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(logoPath);
                    return File(fileBytes, "image/png");
                }
                else
                {
                    // Return a simple placeholder or 404
                    return HttpNotFound("Logo file not found");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error loading company logo: {0}", ex);
                return HttpNotFound("Error loading logo");
            }
        }
        // ── Error Pages (referenced by customErrors in Web.config) ──────────
        [AllowAnonymous]
        public ActionResult Error()
        {
            Response.StatusCode = 500;
            return View("~/Views/Error/Index.cshtml");
        }

        [AllowAnonymous]
        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            return View("~/Views/Error/NotFound.cshtml");
        }

        [AllowAnonymous]
        public ActionResult Forbidden()
        {
            Response.StatusCode = 403;
            return View("~/Views/Error/Forbidden.cshtml");
        }
    }
}
