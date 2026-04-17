using System;
using System.Security.Cryptography;
using System.Text;
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
        public ActionResult Login(string username, string password, string captcha, string role, string returnUrl)
        {
            return HandleLoginPost(username, password, captcha, role, returnUrl);
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
        public ActionResult Profile()
        {
            try
            {
                var username = User.Identity.Name;
                var lowerUsername = username.ToLower();
                var user = _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);

                if (user == null) return HttpNotFound();

                var viewModel = new ProfileViewModel
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Phone = user.Phone,
                    UserName = user.UserName,
                    Role = user.Role,
                    CompanyName = user.Company != null ? user.Company.Name : "System Global",
                    IsEmailVerified = user.IsEmailVerified
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                return Content("Exception in Profile: " + ex.Message + " | Trace: " + ex.StackTrace);
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult Profile(ProfileViewModel model)
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
        public ActionResult Register(int? companyId = null, bool isSuperAdmin = false)
        {
            return HandleRegisterGet(companyId, isSuperAdmin);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public ActionResult Register(RegisterViewModel model, bool isSuperAdmin = false)
        {
            return HandleRegisterPost(model, isSuperAdmin);
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

        /// <summary>
        /// Computes a short SHA256 fingerprint of the User-Agent string.
        /// Used to bind auth cookies to the issuing browser for cookie theft detection.
        /// </summary>
        private string ComputeUaHash(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "unknown";
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(userAgent));
                return Convert.ToBase64String(hash).Substring(0, 16);
            }
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
        public JsonResult SendSetupCode(string method)
        {
            string username = Session["ForcedMfaSetup"] as string;
            if (string.IsNullOrEmpty(username)) return Json(new { success = false, message = "Session expired" });

            var user = _uow.Context.Users.FirstOrDefault(u => u.UserName == username);
            if (user == null) return Json(new { success = false, message = "User not found" });

            SendMfaCode(user, method);
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

                var lowerUsername = username.ToLower();
                var user = _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
                if (user == null) return RedirectToAction("Login");

                ViewBag.MfaMethod = user.MfaMethod ?? "App";
                ViewBag.EmailHint = MaskContactInfo(user.Email);

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

            var user = _uow.Context.Users.FirstOrDefault(u => u.UserName == username);
            if (user == null) return Json(new { success = false, message = "User not found" });

            SendMfaCode(user);
            return Json(new { success = true });
        }

        private void SendMfaCode(User user)
        {
            SendMfaCode(user, null);
        }

        private void SendMfaCode(User user, string overrideMethod)
        {
            string method = overrideMethod ?? user.MfaMethod;
            if (method != "Email") return;

            string code = SecuritySvc.GenerateTemporaryCode();
            user.TwoFactorCode = code;
            user.TwoFactorExpiry = DateTime.Now.AddMinutes(10);
            _uow.Users.Update(user);
            _uow.Complete();

            if (method == "Email")
            {
                var userEmail = user.Email;
                var securityToken = code;
                Task.Run(async () => {
                    try {
                        var emailSvc = new EmailService(new SettingsService());
                        await emailSvc.SendMfaCodeEmailAsync(userEmail, securityToken);
                    } catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine("--- [MFA EMAIL ERROR] Failed to send: " + ex.Message);
                        System.Diagnostics.Trace.WriteLine("--- [MFA EMAIL ERROR] Failed to send: " + ex.Message);
                    }
                });
            }
        }

        private string MaskContactInfo(string email)
        {
            if (string.IsNullOrEmpty(email)) return "Not configured";
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            var name = parts[0];
            if (name.Length <= 2) return email;
            return name.Substring(0, 2) + "****@ " + parts[1];
        }

        private ActionResult CompleteLogin(User user)
        {
            return CompleteUserLogin(user);
        }
    }
}
