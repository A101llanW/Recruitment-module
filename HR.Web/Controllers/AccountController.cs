using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Helpers;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AccountController : Controller
    {
        // Captcha image helper class
        public class CaptchaImage
        {
            public int Index { get; set; }
            public string Base64 { get; set; }
            public string Label { get; set; }
        }
        private readonly UnitOfWork _uow = new UnitOfWork();
        private SecurityService _securityService;
        private AuditService _auditService;
        private IEmailService _emailService;

        private SecurityService SecuritySvc => _securityService ?? (_securityService = new SecurityService());
        private AuditService AuditSvc => _auditService ?? (_auditService = new AuditService());
        private IEmailService EmailSvc => _emailService ?? (_emailService = new EmailService());
        private RealisticCaptchaService _captchaService = new RealisticCaptchaService();

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            // If no tenant is specified in the URL, check if we have a remembered company
            var urlTenantToken = RouteData.Values["tenant"] as string;
            ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(urlTenantToken);
            if (string.IsNullOrEmpty(urlTenantToken))
            {
                var preferredTenantCookie = Request.Cookies["PreferredTenant"];
                var preferredTenant = preferredTenantCookie != null ? preferredTenantCookie.Value : null;
                if (!string.IsNullOrEmpty(preferredTenant))
                {
                    var company = _uow.Companies.GetAll().FirstOrDefault(c => c.Slug == preferredTenant && c.IsActive);
                    if (company != null)
                    {
                        ViewBag.PreferredCompanyName = company.Name;
                        ViewBag.PreferredCompanySlug = company.Slug;
                    }
                }
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        [ValidateInput(false)]
        public ActionResult Login(string username, string password, string captcha, string role, string returnUrl, bool acceptLegalTerms = false, string legalRelationship = null)
        {
            _ = acceptLegalTerms;
            _ = legalRelationship;
            return HandleLoginPost(username, password, captcha, role, returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        [ValidateInput(false)]
        public ActionResult ConfirmLegalConsent(bool acceptLegalTerms = false)
        {
            return HandleConfirmLegalConsent(acceptLegalTerms);
        }

        [Authorize]
        public ActionResult ChangePassword()
        {
            // Check if user is required to change password
            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie != null)
            {
                var ticket = FormsAuthentication.Decrypt(authCookie.Value);
                if (ticket != null && ticket.UserData.Contains("RequirePasswordChange"))
                {
                    ViewBag.ForcePasswordChange = true;
                    ViewBag.Message = TempData["PasswordChangeMessage"] as string;
                }
            }
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            return HandleChangePassword(model);
        }

        [Authorize]
        public new ActionResult Profile()
        {
            try
            {
                var user = GetCurrentUserFromIdentity(User.Identity.Name);
                if (user == null) return HttpNotFound();

                return View(BuildProfileViewModel(user));
            }
            catch (Exception ex)
            {
                return Content("Exception in Profile: " + ex.Message + " | Trace: " + ex.StackTrace);
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public new ActionResult Profile(ProfileViewModel model)
        {
            return HandleProfileUpdate(model);
        }

        private User GetCurrentUserFromIdentity(string username)
        {
            return ResolveCurrentUserFromIdentity(username);
        }

        [HttpGet]
        [Authorize]
        public ActionResult VerifyEmail()
        {
            var username = User.Identity.Name;
            var user = GetCurrentUserFromIdentity(username);

            if (user == null) return HttpNotFound();
            
            // If already verified, move them to dashboard
            if (user.IsEmailVerified) {
                var tenantToken = RouteData.Values["tenant"] as string;
                return RedirectToAction("Index", "Dashboard", new { tenant = tenantToken });
            }

            return View(user);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateAndSendVerification(string newEmail)
        {
            var username = User.Identity.Name;
            var user = GetCurrentUserFromIdentity(username);

            if (user == null) return HttpNotFound();
            
            if (string.IsNullOrWhiteSpace(newEmail))
            {
                TempData["ErrorMessage"] = "Email address is required.";
                return RedirectToAction("VerifyEmail");
            }

            AuditSvc.LogAction(username, "EMAIL_VERIFICATION_EMAIL_SUBMITTED", "Account", user.Id.ToString(),
                "User submitted email for verification", new { newEmail = newEmail, tenant = (string)RouteData.Values["tenant"] });

            user.Email = newEmail.Trim();
            
            // Generate OTP
            string otp = SecuritySvc.GenerateTemporaryCode();
            user.EmailVerificationCode = otp;
            user.EmailVerificationExpiry = DateTime.Now.AddMinutes(15);
            
            _uow.Users.Update(user);
            _uow.Complete();

            try
            {
                await EmailSvc.SendEmailVerificationOtpAsync(user.Email, otp);
                System.Diagnostics.Debug.WriteLine(string.Format("--- [EMAIL VERIFICATION OTP] Sent to {0}: {1} ---", user.Email, otp));
                
                TempData["SuccessMessage"] = "Verification code sent to your email.";
                return RedirectToAction("VerifyEmail");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to send email: " + ex.Message;
                AuditSvc.LogAction(username, "EMAIL_VERIFICATION_SEND_FAIL", "Account", user.Id.ToString(), 
                    "Failed to send email verification OTP: " + ex.Message);
                return RedirectToAction("VerifyEmail");
            }
        }



        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendVerificationEmail()
        {
            var username = User.Identity.Name;
            var user = GetCurrentUserFromIdentity(username);

            if (user == null) return HttpNotFound();
            if (user.IsEmailVerified) return Json(new { success = false, message = "Email is already verified." });

            // Generate 6-digit OTP
            string otp = SecuritySvc.GenerateTemporaryCode();
            user.EmailVerificationCode = otp;
            user.EmailVerificationExpiry = DateTime.Now.AddMinutes(15);
            
            _uow.Users.Update(user);
            _uow.Complete();

            try
            {
                await EmailSvc.SendEmailVerificationOtpAsync(user.Email, otp);
                
                // For development, also log it
                System.Diagnostics.Debug.WriteLine(string.Format("--- [EMAIL VERIFICATION OTP] Sent to {0}: {1} ---", user.Email, otp));
                
                return Json(new { success = true, message = "Verification code sent to your email." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to send verification email: " + ex.Message);
                AuditSvc.LogAction(username, "EMAIL_VERIFICATION_SEND_FAIL", "Account", user.Id.ToString(), 
                    "Failed to send email verification OTP: " + ex.Message);
                return Json(new { success = false, message = "Failed to send email. Please try again later." });
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyEmailSubmit(string code)
        {
            return HandleVerifyEmailSubmit(code);
        }



        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyEmail(string code)
        {
            if (string.IsNullOrEmpty(code)) return Json(new { success = false, message = "Please enter the verification code." });

            var username = User.Identity.Name;
            var user = GetCurrentUserFromIdentity(username);

            if (user == null) return HttpNotFound();

            if (user.EmailVerificationCode == code && user.EmailVerificationExpiry > DateTime.Now)
            {
                user.IsEmailVerified = true;
                user.EmailVerificationCode = null;
                user.EmailVerificationExpiry = null;
                
                _uow.Users.Update(user);
                _uow.Complete();

                AuditSvc.LogAction(username, "EMAIL_VERIFIED", "Account", user.Id.ToString(), "User verified their email address.");
                
                return Json(new { success = true, message = "Email verified successfully!" });
            }

            return Json(new { success = false, message = "Invalid or expired verification code." });
        }

        [Authorize]
        public ActionResult Logout()
        {
            var username = User.Identity.Name;
            
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();
            
            AuditSvc.LogLogout(username);
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult LicenseExpired()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Register(int? companyId = null, bool isSuperAdmin = false, string returnUrl = null)
        {
            return HandleRegisterGet(companyId, isSuperAdmin, returnUrl);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public ActionResult Register(RegisterViewModel model, bool isSuperAdmin = false, string returnUrl = null)
        {
            return HandleRegisterPost(model, isSuperAdmin, returnUrl);
        }

        // Forgot Password Actions
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            return await HandleForgotPassword(model);
        }

        [AllowAnonymous]
        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.ErrorMessage = "Invalid password reset token.";
                return View();
            }

            // Validate token
            var passwordReset = _uow.PasswordResets.GetAll()
                .FirstOrDefault(t => t.Token == token && !t.IsUsed && t.ExpiryDate > DateTime.UtcNow);

            if (passwordReset == null)
            {
                ViewBag.ErrorMessage = "This password reset link is invalid or has expired.";
                return View();
            }

            var model = new ResetPasswordViewModel { Token = token };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            return HandleResetPassword(model);
        }

        // MFA Setup
        [HttpGet]
        public ActionResult SetupMFA()
        {
            try 
            {
                string username = Session["ForcedMfaSetup"] as string;
                if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");

                var user = _uow.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null) return RedirectToAction("Login");

                // Prepare setup data for Email
                ViewBag.Email = user.Email;
                return View();
            }
            catch (Exception ex)
            {
                AuditSvc.LogAction("SYSTEM", "MFA_SETUP_ERROR", "Account", "", 
                    errorMessage: ex.Message + " | Stack: " + ex.StackTrace);
                throw; // Rethrow to show error page, but now we have it in AuditLogs
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupMFA(string method, string code)
        {
            string username = Session["ForcedMfaSetup"] as string;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");

            var user = _uow.Context.Users.FirstOrDefault(u => u.UserName == username);
            if (user == null) return RedirectToAction("Login");

            bool isValid = false;

            isValid = SecuritySvc.ValidateTemporaryCode(user, code);

            if (isValid)
            {
                user.IsTwoFactorEnabled = true;
                user.MfaMethod = method;
                user.TwoFactorCode = null; // Clear it
                _uow.Users.Update(user);
                _uow.Complete();

                Session.Remove("ForcedMfaSetup");
                return CompleteLogin(user);
            }

            ModelState.AddModelError("", "Invalid verification code. Please try again.");
            
            // Re-render setup data
            ViewBag.Email = user.Email;
            ViewBag.SelectedMethod = "Email";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SendSetupCode(string method)
        {
            string username = Session["ForcedMfaSetup"] as string;
            if (string.IsNullOrEmpty(username)) return Json(new { success = false, message = "Session expired" });

            var user = _uow.Context.Users.FirstOrDefault(u => u.UserName == username);
            if (user == null) return Json(new { success = false, message = "User not found" });

            if (!SendMfaCode(user, method))
            {
                return Json(new { success = false, message = "Failed to send verification email. Check SMTP configuration." });
            }

            return Json(new { success = true });
        }

        // MFA Verification
        [HttpGet]
        public ActionResult VerifyMFA()
        {
            try 
            {
                string username = Session["PendingMfaUsername"] as string ?? (User.Identity.IsAuthenticated ? User.Identity.Name : null);
                if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");

                var user = FindPendingMfaUser(username);
                if (user == null) return RedirectToAction("Login");

                ViewBag.MfaMethod = user.MfaMethod ?? "Email";
                ViewBag.EmailHint = MaskContactInfo(user.Email);

                if (UsesEmailMfa(user))
                {
                    if (!HasActiveMfaCode(user))
                    {
                        if (!SendMfaCode(user))
                        {
                            ViewBag.MfaSendError = "We could not send a verification email. Check SMTP settings or use Resend below.";
                        }
                    }
                }

                return View();
            }
            catch (Exception ex)
            {
                AuditSvc.LogAction("SYSTEM", "MFA_VERIFY_ERROR", "Account", "", 
                    errorMessage: ex.Message + " | Stack: " + ex.StackTrace);
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyMFA(string code)
        {
            return HandleVerifyMfaSubmission(code);
        }

        [HttpPost]
        public JsonResult ResendCode()
        {
            string username = Session["PendingMfaUsername"] as string;
            if (string.IsNullOrEmpty(username)) return Json(new { success = false, message = "Session expired" });

            var user = FindPendingMfaUser(username);
            if (user == null) return Json(new { success = false, message = "User not found" });

            if (!UsesEmailMfa(user))
            {
                return Json(new { success = false, message = "Email verification is not enabled for this account." });
            }

            if (!SendMfaCode(user))
            {
                return Json(new { success = false, message = "Failed to send verification email. Check SMTP configuration." });
            }

            return Json(new { success = true });
        }

        private User FindPendingMfaUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            var companyId = LegalConsentSession.TryReadCompanyId(Session);
            var lowerUsername = username.ToLower();
            var userQuery = _uow.Context.Users.Where(u => u.UserName.ToLower() == lowerUsername);
            if (companyId.HasValue)
            {
                userQuery = userQuery.Where(u => u.CompanyId == companyId.Value);
            }

            return userQuery.FirstOrDefault();
        }

        private static bool UsesEmailMfa(User user)
        {
            return user != null &&
                string.Equals(user.MfaMethod, "Email", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasActiveMfaCode(User user)
        {
            return user != null &&
                !string.IsNullOrEmpty(user.TwoFactorCode) &&
                user.TwoFactorExpiry.HasValue &&
                user.TwoFactorExpiry.Value > DateTime.Now;
        }

        private bool SendMfaCode(User user)
        {
            return SendMfaCode(user, null);
        }

        private bool SendMfaCode(User user, string overrideMethod)
        {
            string method = overrideMethod ?? user.MfaMethod;
            if (!string.Equals(method, "Email", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                System.Diagnostics.Trace.WriteLine("--- [MFA EMAIL ERROR] User has no email address ---");
                return false;
            }

            string code = SecuritySvc.GenerateTemporaryCode();
            user.TwoFactorCode = code;
            user.TwoFactorExpiry = DateTime.Now.AddMinutes(10);
            _uow.Users.Update(user);
            _uow.Complete();

            DevDiagnostics.LogOneTimeCode("MFA CODE", user.Email, code);

            try
            {
                EmailSvc.SendMfaCodeEmailAsync(user.Email.Trim(), code).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                DevDiagnostics.LogOneTimeCode("MFA CODE (email failed — use code above)", user.Email, code);
                System.Diagnostics.Debug.WriteLine("--- [MFA EMAIL ERROR] Failed to send: " + ex.Message);
                System.Diagnostics.Trace.WriteLine("--- [MFA EMAIL ERROR] Failed to send: " + ex.Message);
                AuditSvc.LogAction(user.UserName, "MFA_EMAIL_SEND_FAILED", "Account", user.Id.ToString(), ex.Message);
                return false;
            }
        }

        private string MaskContactInfo(string email)
        {
            if (string.IsNullOrEmpty(email)) return "Not configured";
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            var name = parts[0];
            if (name.Length <= 2) return email;
            return name.Substring(0, 2) + "****@" + parts[1];
        }

        private ActionResult CompleteLogin(User user)
        {
            return CompleteUserLogin(user);
        }
    }
}
