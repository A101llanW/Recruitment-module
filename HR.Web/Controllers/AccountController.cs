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
    public class AccountController : Controller
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
            try 
            {
                var clientIP = Request.UserHostAddress;
            
            // 1. Identify Tenant Context
            var urlTenantToken = RouteData.Values["tenant"] as string;
            int? targetCompanyId = null;
            if (!string.IsNullOrEmpty(urlTenantToken))
            {
                var targetCompany = _uow.Companies.GetAll().FirstOrDefault(c => c.Slug == urlTenantToken);
                if (targetCompany != null)
                {
                    targetCompanyId = targetCompany.Id;
                }
            }

            // 2. Validate Captcha
            string sessionCaptchaText = Session["CaptchaText"] as string;
            DateTime? sessionExpiry = Session["CaptchaExpiry"] as DateTime?;
            string sessionCaptchaId = Session["CaptchaId"] as string;
            
            if (string.IsNullOrEmpty(sessionCaptchaText) || !sessionExpiry.HasValue || string.IsNullOrEmpty(sessionCaptchaId))
            {
                ModelState.AddModelError("", "CAPTCHA session expired. Please try again.");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }
            
            if (DateTime.UtcNow > sessionExpiry.Value)
            {
                // Clear expired captcha
                Session.Remove("CaptchaText");
                Session.Remove("CaptchaExpiry");
                Session.Remove("CaptchaId");
                ModelState.AddModelError("", "CAPTCHA expired. Please try again.");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }
            
            if (string.IsNullOrEmpty(captcha) || !string.Equals(captcha, sessionCaptchaText, StringComparison.Ordinal))
            {
                ModelState.AddModelError("", "Invalid security code. Please try again.");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            // Captcha validated; clear it so it can't be reused.
            Session.Remove("CaptchaText");
            Session.Remove("CaptchaExpiry");
            Session.Remove("CaptchaId");

            // 3. Basic Validation
            var isGlobalSuperAdmin = !string.IsNullOrEmpty(username) && string.Equals(username, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(urlTenantToken);
            if (string.IsNullOrWhiteSpace(username))
            {
                ModelState.AddModelError("", "Username is required.");
                SecuritySvc.RecordLoginAttempt(username, clientIP, false, targetCompanyId, "Username required");
                AuditSvc.LogLogin(username, false, "Username required");
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Password is required.");
                SecuritySvc.RecordLoginAttempt(username, clientIP, false, targetCompanyId, "Password required");
                AuditSvc.LogLogin(username, false, "Password required");
                return View();
            }

            // ── IP-Level Rate Limiting ──────────────────────────────────────────────
            // Blocks an IP that cycles through multiple usernames (credential stuffing).
            // Per-username lockout alone doesn't catch this attack pattern.
            const int maxIPFailures     = 20;
            const int ipWindowMinutes   = 15;
            var windowStart = DateTime.Now.AddMinutes(-ipWindowMinutes);
            var ipFailureCount = _uow.LoginAttempts.GetAll()
                .Count(a => a.IPAddress == clientIP
                         && !a.WasSuccessful
                         && a.AttemptTime > windowStart);

            if (ipFailureCount >= maxIPFailures)
            {
                AuditSvc.LogAction("ANONYMOUS", "IP_RATE_LIMITED", "Account", "",
                    string.Format("IP {0} blocked after {1} failed attempts in {2} minutes", clientIP, ipFailureCount, ipWindowMinutes));
                ModelState.AddModelError("", "Too many failed login attempts from your location. Please wait 15 minutes before trying again.");
                return View();
            }
            // ────────────────────────────────────────────────────────────────────────

            // ────────────────────────────────────────────────────────────────────────
            // A. Identify Tenant Context (Already resolved above)

            // B. Resolve candidates (Pre-fetch to help with lockout disambiguation)
            username = username.Trim();
            var lowerUsername = username.ToLower();
            bool isEmail = username.Contains("@");
            var discoveryQuery = _uow.Context.Users.AsQueryable();
            if (targetCompanyId.HasValue)
            {
                discoveryQuery = discoveryQuery.Where(u => u.CompanyId == targetCompanyId.Value);
            }

            List<User> candidates;
            if (isEmail)
            {
                candidates = discoveryQuery.Where(u => u.Email != null && u.Email.ToLower() == lowerUsername).ToList();
            }
            else
            {
                candidates = discoveryQuery.Where(u => u.UserName != null && u.UserName.ToLower() == lowerUsername).ToList();
            }

            var primaryUser = candidates.FirstOrDefault();
            int? effectiveCompanyId = targetCompanyId ?? (candidates.Count == 1 ? candidates[0].CompanyId : null);

            // C. Check Lockdown Status
            if (!isGlobalSuperAdmin && SecuritySvc.IsAccountLocked(username, effectiveCompanyId))
            {
                var lockoutEndTime = SecuritySvc.GetLockoutEndTime(username, effectiveCompanyId);
                var remainingTime = lockoutEndTime.HasValue 
                    ? lockoutEndTime.Value - DateTime.Now 
                    : TimeSpan.Zero;
                
                ModelState.AddModelError("", string.Format("Account is locked. Please try again in {0} minutes.", remainingTime.Minutes));
                SecuritySvc.RecordLoginAttempt(username, clientIP, false, effectiveCompanyId, "Account locked");
                AuditSvc.LogLogin(username, false, string.Format("Account locked. Try again in {0} minutes", remainingTime.Minutes));
                return View();
            }

            // D. Disambiguate Identity (Handle multi-company email discovery)
            if (!isEmail && candidates.Count > 1 && !targetCompanyId.HasValue)
            {
                ModelState.AddModelError("", "This username is used by multiple companies. Please use your email address to help us find the right account.");
                return View();
            }

            if (candidates.Count > 1 && !targetCompanyId.HasValue)
            {
                ViewBag.MultiCandidates = candidates;
                ModelState.AddModelError("", "We found multiple accounts for this email. Please select the correct portal below.");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            var user = primaryUser;

            if (user == null)
            {
                // Generic message — do not reveal whether the identifier exists
                ModelState.AddModelError("", "Invalid username or password.");
                SecuritySvc.RecordLoginAttempt(username, clientIP, false, targetCompanyId, "Identifier not found");
                AuditSvc.LogLogin(username, false, "Invalid identifier: " + username);
                return View();
            }

            // Record the actual username if they used email to login
            var actualUsername = user.UserName;

            // Verify the password against stored password hash
            bool isValidPassword = false;
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                isValidPassword = PasswordHelper.VerifyPassword(user.PasswordHash, password);
            }

            if (!isValidPassword)
            {
                var remainingAttempts = SecuritySvc.GetRemainingAttempts(username, user.CompanyId);
                var warningMessage = remainingAttempts > 1
                    ? string.Format("Invalid username or password. {0} attempts remaining.", remainingAttempts)
                    : string.Format("Invalid username or password. {0} attempt remaining before account lockout.", remainingAttempts);

                if (!isGlobalSuperAdmin)
                {
                    SecuritySvc.RecordLoginAttempt(username, clientIP, false, user.CompanyId, "Invalid password");
                }

                ModelState.AddModelError("", warningMessage);
                AuditSvc.LogLogin(username, false, "Invalid password");
                return View();
            }

            // 1. Identify User Role and Association
            var userRole = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            var isSuperAdmin = !user.CompanyId.HasValue && (
                string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(userRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            );
            
            if (isSuperAdmin) { userRole = "SuperAdmin"; }

            // 2. Resolve Correct Tenant Slug
            string correctTenantSlug = null;
            if (!isSuperAdmin && user.CompanyId.HasValue)
            {
                var company = _uow.Companies.Get(user.CompanyId.Value);
                if (company != null && company.IsActive)
                {
                    correctTenantSlug = company.Slug;
                }
            }

            // 3. Authenticate and Prepare Redirect
            AuditSvc.LogAction(username, "LOGIN_START_AUTH", "Account", user.Id.ToString(), true, "Credentials valid, processing session");
            
            SecuritySvc.RecordLoginAttempt(username, clientIP, true, user.CompanyId);
            SecuritySvc.ClearFailedAttempts(username, user.CompanyId);
            AuditSvc.LogLogin(username, true);

            // Ensure user has a security token (AccessToken) for session validation
            if (string.IsNullOrEmpty(user.AccessToken))
            {
                user.AccessToken = SecuritySvc.GenerateSecureToken();
                _uow.Users.Update(user);
                _uow.Complete();
                AuditSvc.LogAction(username, "LOGIN_TOKEN_GEN", "Account", user.Id.ToString(), true, "New AccessToken generated");
            }

            // Set Auth Cookie with token-based session validation data
            // Structure: Role|CompanyId|AccessToken|UAHash
            var uaHash = ComputeUaHash(Request.UserAgent);
            var userData = string.Format("{0}|{1}|{2}|{3}", 
                userRole, 
                user.CompanyId, 
                user.AccessToken,
                uaHash);

            var ticket = new FormsAuthenticationTicket(
                1, 
                actualUsername, 
                DateTime.Now, 
                DateTime.Now.AddHours(8), 
                false, 
                userData);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,                    // Inaccessible to JavaScript (XSS protection)
                Secure   = Request.IsSecureConnection // Only sent over HTTPS when available
            };
            Response.Cookies.Add(cookie);
            AuditSvc.LogAction(username, "LOGIN_COOKIE_SET", "Account", user.Id.ToString(), true, "Auth cookie added to response");

            // 4. Force Email Verification if not already verified
            if (!user.IsEmailVerified)
            {
                AuditSvc.LogAction(username, "LOGIN_REDIRECT_EMAIL_VERIFY", "Account", user.Id.ToString(), true, "Redirecting to email verification");
                // ... (rest of otp logic)
                string otpCode = SecuritySvc.GenerateTemporaryCode();
                user.EmailVerificationCode = otpCode;
                user.EmailVerificationExpiry = DateTime.Now.AddMinutes(15);
                _uow.Users.Update(user);
                _uow.Complete();

                // Fire-and-forget email sending - avoid HttpContext dependencies in background thread
                var userEmail = user.Email;
                var securityToken = otpCode;
                
                System.Threading.Tasks.Task.Run(async () => {
                    try {
                        // Create services without HttpContext dependencies
                        var emailSvc = new EmailService(new SettingsService());
                        await emailSvc.SendEmailVerificationOtpAsync(userEmail, securityToken);
                    } catch (Exception ex) {
                        // Log error without HttpContext-dependent services
                        System.Diagnostics.Debug.WriteLine("--- [EMAIL VERIFICATION ERROR] Failed to send: " + ex.Message);
                        System.Diagnostics.Trace.WriteLine("--- [EMAIL VERIFICATION ERROR] Failed to send: " + ex.Message);
                    }
                });

                return RedirectToAction("VerifyEmail", "Account", new { tenant = correctTenantSlug });
            }

            // 5. Handle Redirection Logic
            if (isSuperAdmin || string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                // BOTH SuperAdmins and Company Admins MUST use MFA
                if (user.IsTwoFactorEnabled)
                {
                    AuditSvc.LogAction(username, "LOGIN_REDIRECT_MFA", "Account", user.Id.ToString(), true, "Redirecting to MFA challenge");
                    // Redirect to MFA challenge
                    Session["PendingMfaUsername"] = actualUsername;

                    // If they use Email, trigger the first code automatically
                    if (user.MfaMethod == "Email")
                    {
                        SendMfaCode(user);
                    }

                    return RedirectToAction("VerifyMFA");
                }
                else
                {
                    // MFA not set up yet
                    // Force setup before allowing access
                    Session["ForcedMfaSetup"] = actualUsername;
                    return RedirectToAction("SetupMFA");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(correctTenantSlug))
                {
                    ModelState.AddModelError("", "Your account is not associated with an active company.");
                    return View();
                }

                // Remember tenant preference
                var prefCookie = new HttpCookie("PreferredTenant", correctTenantSlug) { Expires = DateTime.Now.AddDays(30), Path = "/" };
                Response.Cookies.Add(prefCookie);

                // If user is on the wrong tenant URL OR global URL, redirect to their correct branded portal
                if (string.IsNullOrEmpty(urlTenantToken) || !string.Equals(urlTenantToken, correctTenantSlug, StringComparison.OrdinalIgnoreCase))
                {
                    AuditSvc.LogAction(username, "LOGIN_REDIRECT_TENANT", "Account", user.Id.ToString(), true, "Redirecting to branded portal: " + correctTenantSlug);
                    return RedirectToAction("Index", "Positions", new { tenant = correctTenantSlug });
                }

                // If there is a safe, approved in-app return target, use it.
                var safeReturnResult = BuildSafeReturnRedirect(returnUrl, correctTenantSlug);
                if (safeReturnResult != null)
                {
                    AuditSvc.LogAction(username, "LOGIN_REDIRECT_RETURNURL", "Account", user.Id.ToString(), true, "Redirecting to validated return target");
                    return safeReturnResult;
                }

                // Default home for regular users
                AuditSvc.LogAction(username, "LOGIN_REDIRECT_DEFAULT", "Account", user.Id.ToString(), true, "Redirecting to default dashboard for " + correctTenantSlug);
                return RedirectToAction("Index", "Positions", new { tenant = correctTenantSlug });
            }
        }
        catch (Exception ex)
        {
                AuditSvc.LogAction(username, "LOGIN_CRASH", "Account", "", 
                    wasSuccessful: false, errorMessage: "CRASH: " + ex.Message + " | Stack: " + ex.StackTrace);
                ModelState.AddModelError("", "A system error occurred. Our team has been notified.");
                return View();
            }
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            var user = _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
            
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(model);
            }

            // Check if this is a forced password change
            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            bool isForcedChange = false;
            
            if (authCookie != null)
            {
                var ticket = FormsAuthentication.Decrypt(authCookie.Value);
                if (ticket != null && ticket.UserData.Contains("RequirePasswordChange"))
                {
                    isForcedChange = true;
                }
            }

            // For forced password changes, skip current password verification
            if (!isForcedChange)
            {
                // Verify current password
                if (!PasswordHelper.VerifyPassword(user.PasswordHash, model.CurrentPassword))
                {
                    ModelState.AddModelError("", "Current password is incorrect.");
                    AuditSvc.LogAction(username, "PASSWORD_CHANGE_FAILED", "Account", user.Id.ToString(), 
                        "Current password verification failed");
                    return View(model);
                }
            }

            // Check if new password meets security requirements
            if (!PasswordHelper.IsPasswordStrong(model.NewPassword))
            {
                ModelState.AddModelError("", PasswordHelper.GetPasswordStrengthMessage());
                return View(model);
            }

            // Check if new password is different from current password
            if (PasswordHelper.VerifyPassword(user.PasswordHash, model.NewPassword))
            {
                ModelState.AddModelError("", "New password must be different from current password.");
                return View(model);
            }

            try
            {
                // Update password
                user.PasswordHash = PasswordHelper.HashPassword(model.NewPassword);
                user.RequirePasswordChange = false;
                user.LastPasswordChange = DateTime.Now;
                user.PasswordChangeExpiry = null;
                
                _uow.Users.Update(user);
                _uow.Complete();

                // Log successful password change
                AuditSvc.LogAction(username, "PASSWORD_CHANGED", "Account", user.Id.ToString(), 
                    "Password successfully changed to meet security requirements");

                // Preserve forced-change messaging for users who were required to rotate credentials.
                bool wasForcedChange = isForcedChange;

                if (wasForcedChange)
                {
                    TempData["SuccessMessage"] = "Your password has been successfully updated! You can now access the system with your new secure password.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Your password has been successfully updated!";
                }
                
                // Rebuild cookie + redirect via the same normalized role flow used after login.
                // This prevents SuperAdmins (Role=Admin + CompanyId=null) from being treated as company admins.
                return CompleteLogin(user);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while changing your password. Please try again.");
                AuditSvc.LogAction(username, "PASSWORD_CHANGE_ERROR", "Account", user.Id.ToString(), 
                    "Password change failed: " + ex.Message);
                return View(model);
            }
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
            if (!ModelState.IsValid) return View(model);

            var username = User.Identity.Name;
            var lowerUsername = username.ToLower();
            var user = _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);

            if (user == null) return HttpNotFound();

            var oldFirstName = user.FirstName;
            var oldLastName = user.LastName;
            var oldEmail = user.Email;
            var oldPhone = user.Phone;

            // Check if email changed and is unique
            if (user.Email != model.Email)
            {
                var existingUser = _uow.Context.Users.FirstOrDefault(u => u.Email == model.Email && u.Id != user.Id);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "This email address is already in use by another account.");
                    return View(model);
                }

                // Identity changed, invalidate all existing sessions
                user.AccessToken = SecuritySvc.GenerateSecureToken();

                // Reset verification status since email has changed
                user.IsEmailVerified = false;
                user.EmailVerificationCode = null;
                user.EmailVerificationExpiry = null;
            }

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;
            user.Phone = model.Phone;

            _uow.Users.Update(user);
            _uow.Complete();

            // Renew authentication cookie to include the new AccessToken and prevent logout
            var userRole = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            var uaHash = ComputeUaHash(Request.UserAgent);
            var userData = string.Format("{0}|{1}|{2}|{3}", 
                userRole, 
                user.CompanyId, 
                user.AccessToken,
                uaHash);

            var ticket = new FormsAuthenticationTicket(
                1, 
                username, 
                DateTime.Now, 
                DateTime.Now.AddHours(8), 
                false, 
                userData);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,
                Secure   = Request.IsSecureConnection
            };
            Response.Cookies.Add(cookie);

            // Sync with Applicant record if it exists
            string syncOldEmail = oldEmail;
            string syncNewEmail = model.Email;

            // Use direct context query but with clearer separation to avoid captures that EF might dislike
            var applicant = _uow.Context.Applicants.FirstOrDefault(a => a.Email == syncOldEmail);
            
            if (applicant == null && syncOldEmail != syncNewEmail)
            {
                applicant = _uow.Context.Applicants.FirstOrDefault(a => a.Email == syncNewEmail);
            }

            if (applicant != null)
            {
                applicant.FullName = string.Format("{0} {1}", model.FirstName, model.LastName);
                applicant.Email = model.Email;
                applicant.Phone = model.Phone;
                _uow.Applicants.Update(applicant);
                _uow.Complete();
            }

            AuditSvc.LogUpdate(username, "Account", user.Id.ToString(),
                new { FirstName = oldFirstName, LastName = oldLastName, Email = oldEmail, Phone = oldPhone },
                new { FirstName = model.FirstName, LastName = model.LastName, Email = model.Email, Phone = model.Phone });

            TempData["SuccessMessage"] = "Your profile has been updated successfully!";
            return RedirectToAction("Profile");
        }

        private User GetCurrentUserFromIdentity(string username)
        {
            var lowerUsername = username.ToLower();
            int? companyId = null;
            
            // Try to extract from FormsIdentity
            var formsIdentity = User.Identity as System.Web.Security.FormsIdentity;
            if (formsIdentity == null && User is System.Web.Security.RolePrincipal rolePrincipal)
            {
                formsIdentity = rolePrincipal.Identity as System.Web.Security.FormsIdentity;
            }
            
            if (formsIdentity != null)
            {
                var props = formsIdentity.Ticket.UserData.Split('|');
                if (props.Length >= 2 && int.TryParse(props[1], out int cId)) companyId = cId;
            }
            
            var user = companyId.HasValue 
                ? _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername && u.CompanyId == companyId.Value)
                : _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername && u.CompanyId == null);
                
            // Powerful Fallback: If scoped query failed (e.g. identity type mismatch), try global lookup.
            // This is safe because we already matched the authenticated username.
            if (user == null)
            {
                user = _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
            }
            
            return user;
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
            var username = User.Identity.Name;
            var user = GetCurrentUserFromIdentity(username);

            if (user == null) return HttpNotFound();

            if (string.IsNullOrEmpty(code) || user.EmailVerificationCode != code)
            {
                TempData["ErrorMessage"] = "Invalid verification code.";
                return RedirectToAction("VerifyEmail");
            }

            if (user.EmailVerificationExpiry < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Verification code has expired.";
                return RedirectToAction("VerifyEmail");
            }

            // Success
            user.IsEmailVerified = true;
            user.EmailVerificationCode = null;
            user.EmailVerificationExpiry = null;

            // Automatically enable Email MFA since they just verified their email
            // This satisfies the 2FA requirement going forward
            if (!user.IsTwoFactorEnabled)
            {
                user.IsTwoFactorEnabled = true;
                user.MfaMethod = "Email";
            }

            _uow.Users.Update(user);
            _uow.Complete();

            AuditSvc.LogAction(username, "EMAIL_VERIFIED", "Account", user.Id.ToString(), "Email verified via login screen");

            // Sync with Applicant if exists
            var userEmail = user.Email;
            var applicant = _uow.Context.Applicants.FirstOrDefault(a => a.Email == userEmail);
            if (applicant != null)
            {
                applicant.IsEmailVerified = true;
                _uow.Applicants.Update(applicant);
                _uow.Complete();
            }

            // 5. Handle Redirection Logic for MFA
            var userRole = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            var isSuperAdmin = !user.CompanyId.HasValue && (
                string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(userRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            );

            if (isSuperAdmin || string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                // Both SuperAdmins and Company Admins are required to use MFA.
                // However, since they JUST verified their email address successfully, 
                // we consider this their MFA token for this session.
                // We clear any pending blocks and redirect them directly to the dashboard.
                Session.Remove("PendingMfaUsername");
                Session.Remove("ForcedMfaSetup");
                Session["MfaVerified"] = true; // Future-proofing

                AuditSvc.LogAction(username, "LOGIN_MFA_BYPASSED", "Account", user.Id.ToString(), true, "MFA challenge seamlessly bypassed for first login after email verification");

                var tToken = RouteData.Values["tenant"] as string;
                if (isSuperAdmin)
                {
                    return RedirectToAction("Index", "Companies", new { tenant = (string)null });
                }
                return RedirectToAction("Index", "Positions", new { tenant = tToken });
            }

            // Redirect to appropriate dashboard based on role
            var tenantToken = RouteData.Values["tenant"] as string;
            if (isSuperAdmin)
            {
                return RedirectToAction("Index", "Companies", new { tenant = (string)null });
            }
            return RedirectToAction("Index", "Positions", new { tenant = tenantToken });
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
            ViewBag.IsSuperAdmin = isSuperAdmin;
            
            var viewModel = new RegisterViewModel
            {
                Companies = isSuperAdmin ? _uow.Companies.GetAll().Where(c => c.IsActive).OrderBy(c => c.Name).ToList() : new List<Company>(),
                CompanyId = isSuperAdmin ? companyId : companyId
            };

            // Show application message if coming from application attempt
            if (TempData["ApplicationMessage"] != null)
            {
                ViewBag.ApplicationMessage = TempData["ApplicationMessage"].ToString();
                string returnUrl = TempData["ReturnUrl"] != null ? TempData["ReturnUrl"].ToString() : null;
                ViewBag.ReturnUrl = returnUrl;

                // Try to extract positionId from returnUrl to auto-select company
                if (!companyId.HasValue && !string.IsNullOrEmpty(returnUrl))
                {
                    try
                    {
                        var queryStart = returnUrl.IndexOf('?');
                        if (queryStart < 0)
                        {
                            throw new InvalidOperationException("No query string in returnUrl.");
                        }

                        var query = HttpUtility.ParseQueryString(returnUrl.Substring(queryStart));
                        var posIdStr = query["positionId"];
                        int posId;
                        if (int.TryParse(posIdStr, out posId))
                        {
                            var position = _uow.Positions.Get(posId);
                            if (position != null)
                            {
                                viewModel.CompanyId = position.CompanyId;
                            }
                        }
                    }
                    catch { /* Best effort */ }
                }
            }
            
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public ActionResult Register(RegisterViewModel model, bool isSuperAdmin = false)
        {
            ViewBag.IsSuperAdmin = isSuperAdmin;
            
            if (!ModelState.IsValid)
            {
                model.Companies = isSuperAdmin ? _uow.Companies.GetAll().Where(c => c.IsActive).OrderBy(c => c.Name).ToList() : new List<Company>();
                return View(model);
            }

            // Enforce company selection only for SuperAdmin
            if (isSuperAdmin && !model.CompanyId.HasValue)
            {
                ModelState.AddModelError("CompanyId", "Company selection is required for SuperAdmin registration.");
                model.Companies = new List<Company>(); // Don't show companies for regular users
                return View(model);
            }
            
            // For regular users, if no CompanyId is provided, try to extract from URL or use default
            if (!isSuperAdmin && !model.CompanyId.HasValue)
            {
                // Try to get company from URL context or return error
                var tenantToken = RouteData.Values["tenant"] as string;
                if (!string.IsNullOrEmpty(tenantToken))
                {
                    var company = _uow.Companies.GetAll().FirstOrDefault(c => c.Slug == tenantToken);
                    if (company != null)
                    {
                        model.CompanyId = company.Id;
                    }
                }
                
                if (!model.CompanyId.HasValue)
                {
                    ModelState.AddModelError("", "Unable to determine company for registration. Please contact support.");
                    model.Companies = new List<Company>();
                    return View(model);
                }
            }

            // Additional email validation
            if (!model.Email.Contains("@") || !model.Email.Split('@').Last().Contains("."))
            {
                ModelState.AddModelError("Email", "Please enter a valid and complete email address.");
                model.Companies = isSuperAdmin ? _uow.Companies.GetAll().Where(c => c.IsActive).OrderBy(c => c.Name).ToList() : new List<Company>();
                return View(model);
            }

            // Case-insensitive checks - Allow duplicate username across different companies
            var usernameTakenInCompany = _uow.Context.Users.Any(u => 
                u.UserName == model.UserName && u.CompanyId == model.CompanyId);

            if (usernameTakenInCompany)
            {
                ModelState.AddModelError("UserName", "This username is already taken within this company.");
                model.Companies = isSuperAdmin ? _uow.Companies.GetAll().Where(c => c.IsActive).OrderBy(c => c.Name).ToList() : new List<Company>();
                return View(model);
            }

            if (_uow.Context.Users.Any(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email address is already registered.");
                model.Companies = isSuperAdmin ? _uow.Companies.GetAll().Where(c => c.IsActive).OrderBy(c => c.Name).ToList() : new List<Company>();
                return View(model);
            }

            // Confirm password
            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "The password and confirmation password do not match.");
                model.Companies = isSuperAdmin ? _uow.Companies.GetAll().Where(c => c.IsActive).OrderBy(c => c.Name).ToList() : new List<Company>();
                return View(model);
            }

            // Password strength
            if (!PasswordHelper.IsPasswordStrong(model.Password))
            {
                ModelState.AddModelError("Password", PasswordHelper.GetPasswordStrengthMessage());
                model.Companies = isSuperAdmin ? _uow.Companies.GetAll().Where(c => c.IsActive).OrderBy(c => c.Name).ToList() : new List<Company>();
                return View(model);
            }

            try
            {
                string defaultRole = "Client";
                // Create User entity
                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    UserName = model.UserName,
                    Email = model.Email,
                    Role = "Client",
                    PasswordHash = PasswordHelper.HashPassword(model.Password),
                    CompanyId = model.CompanyId // Associated with company
                };
                _uow.Users.Add(user);
                _uow.Complete();

                // Also create Applicant record
                var applicant = new Applicant
                {
                    FullName = string.Format("{0} {1}", model.FirstName, model.LastName),
                    Email = model.Email,
                    Phone = model.Phone,
                    CompanyId = model.CompanyId // Associated with company
                };
                _uow.Applicants.Add(applicant);
                _uow.Complete();

                // Log successful registration
                AuditSvc.LogAction(User.Identity.Name, "REGISTER", "Account", user.Id.ToString(), 
                    string.Format("New user registered: {0} {1} ({2}, {3})", user.FirstName, user.LastName, user.UserName, user.Email));

                // Auto-login the newly registered user
                user.AccessToken = SecuritySvc.GenerateSecureToken();
                _uow.Users.Update(user);
                _uow.Complete();

                // Structure: Role|CompanyId|AccessToken|UAHash
                var userData = string.Format("{0}|{1}|{2}|{3}", 
                    "Client", 
                    user.CompanyId, 
                    user.AccessToken,
                    ComputeUaHash(Request.UserAgent));

                var ticket = new FormsAuthenticationTicket(
                    1,
                    model.UserName,
                    DateTime.Now,
                    DateTime.Now.AddHours(8),
                    false,
                    userData,
                    FormsAuthentication.FormsCookiePath);

                var encryptedTicket = FormsAuthentication.Encrypt(ticket);
                var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
                {
                    HttpOnly = true,
                    Secure   = Request.IsSecureConnection
                };
                Response.Cookies.Add(cookie);

                // Remember this company for future visits
                var tenantToken = RouteData.Values["tenant"] as string;
                if (!string.IsNullOrEmpty(tenantToken))
                {
                    var tenantCookie = new HttpCookie("PreferredTenant", tenantToken)
                    {
                        Expires = DateTime.Now.AddDays(30),
                        Path = "/"
                    };
                    Response.Cookies.Add(tenantCookie);
                }
                else if (model.CompanyId.HasValue)
                {
                    var userCompany = _uow.Companies.Get(model.CompanyId.Value);
                    if (userCompany != null)
                    {
                        var tenantCookie = new HttpCookie("PreferredTenant", userCompany.Slug)
                        {
                            Expires = DateTime.Now.AddDays(30),
                            Path = "/"
                        };
                        Response.Cookies.Add(tenantCookie);
                    }
                }

                // Check if there's a return URL (from application attempt)
                var returnUrl = Request.Form["ReturnUrl"];

                var safeRegisterReturn = BuildSafeReturnRedirect(returnUrl, tenantToken);
                if (safeRegisterReturn != null)
                {
                    return safeRegisterReturn;
                }

                return RedirectToAction("Index", "Positions", new { tenant = tenantToken });
            }
            catch (Exception ex)
            {
                // Log the error
                AuditSvc.LogAction(User.Identity.Name, "REGISTER_ERROR", "Account", "", 
                    "Registration failed: " + ex.Message);
                
                ModelState.AddModelError("", "Registration failed. Please try again.");
                return View(model);
            }
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
            // ── IP Rate Limiting ────────────────────────────────────────────────────
            // Max 5 forgot-password requests per IP per 10 minutes.
            // Silently drops excess requests — same response either way to prevent enumeration.
            var clientIP = Request.ServerVariables["HTTP_X_FORWARDED_FOR"] ?? Request.UserHostAddress;
            const int maxForgotRequests = 5;
            const int forgotWindowMinutes = 10;

            var requestWindowStart = DateTime.Now.AddMinutes(-forgotWindowMinutes);
            var recentRequestCount = _uow.LoginAttempts.GetAll()
                .Count(a => a.IPAddress == clientIP
                         && a.FailureReason == "FORGOT_PASSWORD_REQUEST"
                         && a.AttemptTime > requestWindowStart);

            if (recentRequestCount >= maxForgotRequests)
            {
                // Log the rate-limit hit but show the same generic message
                AuditSvc.LogAction("ANONYMOUS", "FORGOT_PASSWORD_RATE_LIMITED", "Account", "",
                    string.Format("IP {0} exceeded forgot-password rate limit", clientIP));
                ViewBag.Message = "If an account with that email exists, a reset link has been sent.";
                return View();
            }

            // Record this attempt for future rate-limit checks
            _uow.LoginAttempts.Add(new LoginAttempt
            {
                Username   = model.Email ?? "unknown",
                IPAddress  = clientIP,
                AttemptTime = DateTime.Now,
                WasSuccessful = false,
                FailureReason = "FORGOT_PASSWORD_REQUEST"
            });
            _uow.Complete();
            // ────────────────────────────────────────────────────────────────────────

            if (ModelState.IsValid)
            {
                try
                {
                    // Find user by email
                    var user = _uow.Context.Users.FirstOrDefault(u => u.Email == model.Email);
                    
                    if (user != null)
                    {
                        // Generate secure token
                        // Generate secure token using service
                        var token = SecuritySvc.GenerateSecureToken();
                        var expiryDate = DateTime.UtcNow.AddHours(24); // 24 hour expiry

                        // Invalidate any existing tokens for this user
                        var existingTokens = _uow.PasswordResets.GetAll().Where(t => t.UserId == user.Id && !t.IsUsed);
                        foreach (var existingToken in existingTokens)
                        {
                            existingToken.IsUsed = true;
                        }

                        // Create new password reset token — store requesting IP for security tracking
                        var requestingIp = Request.ServerVariables["HTTP_X_FORWARDED_FOR"] ?? Request.UserHostAddress;
                        var passwordReset = new PasswordReset
                        {
                            UserId = user.Id,
                            Token = token,
                            ExpiryDate = expiryDate,
                            CreatedDate = DateTime.UtcNow,
                            RequestingIP = requestingIp
                        };

                        _uow.PasswordResets.Add(passwordReset);
                        _uow.Complete();

                        // Generate reset link - maintain tenant context if available
                        var tenantToken = RouteData.Values["tenant"] as string;
                        var resetUrl = Url.Action("ResetPassword", "Account", new { tenant = tenantToken, token = token }, Request.Url.Scheme);
                        
                        // TEMP DEBUG: Log reset URL to debug window for testing
                        System.Diagnostics.Debug.WriteLine("=== PASSWORD RESET LINK ===");
                        System.Diagnostics.Debug.WriteLine("Reset URL: " + resetUrl);
                        System.Diagnostics.Debug.WriteLine("Token: " + token);
                        System.Diagnostics.Debug.WriteLine("User Email: " + user.Email);
                        System.Diagnostics.Debug.WriteLine("========================");
                        
                        // ── Send the reset email ────────────────────────────────────────
                        // NOTE: The reset link is ONLY delivered via email.
                        // It is NOT logged to disk or stored in the audit log
                        // to prevent token exposure via log file theft.
                        if (EmailSvc != null)
                        {
                            await EmailSvc.SendPasswordResetEmailAsync(user.Email, resetUrl);
                        }

                        // Audit: record the request — but NOT the token itself
                        AuditSvc.LogAction(user.UserName, "PASSWORD_RESET_REQUEST", "Account",
                            user.Id.ToString(), null, null, true,
                            string.Format("Reset link sent to {0} from IP {1}",
                                user.Email, Request.ServerVariables["HTTP_X_FORWARDED_FOR"] ?? Request.UserHostAddress));
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No user found with email: " + model.Email);
                        // Log attempt for non-existent user
                        AuditSvc.LogAction("GUEST", "PASSWORD_RESET_ATTEMPT", "Account", "", 
                            null, null, false, "Email not found: " + model.Email);
                    }

                    // Always show success message to prevent email enumeration attacks
                    ViewBag.SuccessMessage = "If an account with that email exists, a password reset link has been sent.";
                    return View(model);
                }
                catch (Exception ex)
                {
                    var errorMessage = ex.Message;
                    if (ex.InnerException != null) errorMessage += " Inner Error: " + ex.InnerException.Message;
                    
                    AuditSvc.LogAction("SYSTEM", "PASSWORD_RESET_ERROR", "Account", "", 
                        "Password reset failed: " + errorMessage);
                    var fullMessage = "An error occurred while processing your request: " + errorMessage;
                    ModelState.AddModelError("", fullMessage);
                    ViewBag.ErrorMessage = fullMessage;
                }
            }

            return View(model);
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
            if (ModelState.IsValid)
            {
                try
                {
                    // Validate token again
                    var passwordReset = _uow.PasswordResets.GetAll()
                        .FirstOrDefault(t => t.Token == model.Token && !t.IsUsed && t.ExpiryDate > DateTime.UtcNow);

                    if (passwordReset == null)
                    {
                        ViewBag.ErrorMessage = "This password reset link is invalid or has expired.";
                        return View(model);
                    }

                    // Get user
                    var user = _uow.Users.Get(passwordReset.UserId);
                    if (user == null)
                    {
                        ViewBag.ErrorMessage = "User not found.";
                        return View(model);
                    }

                    // Validate password strength
                    if (!PasswordHelper.IsPasswordStrong(model.NewPassword))
                    {
                        ModelState.AddModelError("", PasswordHelper.GetPasswordStrengthMessage());
                        return View(model);
                    }

                    // Record completing IP and check against originating IP
                    var completingIp = Request.ServerVariables["HTTP_X_FORWARDED_FOR"] ?? Request.UserHostAddress;
                    passwordReset.CompletedIP = completingIp;
                    bool ipMismatch = !string.IsNullOrEmpty(passwordReset.RequestingIP)
                                  && passwordReset.RequestingIP != completingIp;

                    // Update password
                    user.PasswordHash = PasswordHelper.HashPassword(model.NewPassword);
                    user.RequirePasswordChange = false;
                    user.LastPasswordChange = DateTime.UtcNow;
                    user.PasswordChangeExpiry = null;

                    // Mark token as used
                    passwordReset.IsUsed = true;

                    _uow.Complete();

                    // Log successful password reset (include IP mismatch warning if detected)
                    var resetNote = ipMismatch
                        ? string.Format("Password reset completed. IP MISMATCH: requested from {0}, completed from {1}",
                              passwordReset.RequestingIP, completingIp)
                        : "Password was successfully reset";

                    AuditSvc.LogAction(user.UserName, "PASSWORD_RESET_SUCCESS", "Account",
                        user.Id.ToString(), resetNote);

                    if (ipMismatch)
                    {
                        ViewBag.SuccessMessage = "Your password has been reset. "
                            + "NOTE: This reset was completed from a different location than where it was requested. "
                            + "If you did not initiate this reset, please contact your administrator immediately.";
                    }
                    else
                    {
                        ViewBag.SuccessMessage = "Your password has been successfully reset. You can now login with your new password.";
                    }
                    return RedirectToAction("Login");
                }
                catch (Exception ex)
                {
                    AuditSvc.LogAction("SYSTEM", "PASSWORD_RESET_ERROR", "Account", "", 
                        "Password reset completion failed: " + ex.Message);
                    ModelState.AddModelError("", "An error occurred while resetting your password. Please try again.");
                }
            }

            return View(model);
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

        private ActionResult BuildSafeReturnRedirect(string returnUrl, string tenantSlug)
        {
            Uri parsedUri;
            if (!TryParseSafeLocalReturnUri(returnUrl, out parsedUri))
            {
                return null;
            }

            var segments = SplitPathSegments(parsedUri.AbsolutePath);
            if (segments.Length == 0)
            {
                return null;
            }

            string pathTenant;
            string controllerSegment;
            string actionSegment;
            ResolveRouteSegments(segments, out pathTenant, out controllerSegment, out actionSegment);

            var resolvedTenant = ResolveTenantSlug(tenantSlug, pathTenant);
            return BuildWhitelistedRedirect(controllerSegment, actionSegment, segments.Length, resolvedTenant, parsedUri.Query);
        }

        private bool TryParseSafeLocalReturnUri(string returnUrl, out Uri parsedUri)
        {
            parsedUri = null;
            if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                return false;
            }

            if (returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.StartsWith(@"/\", StringComparison.Ordinal))
            {
                return false;
            }

            return Uri.TryCreate("https://local.test" + returnUrl, UriKind.Absolute, out parsedUri);
        }

        private static string[] SplitPathSegments(string path)
        {
            return (path ?? string.Empty).Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void ResolveRouteSegments(string[] segments, out string pathTenant, out string controllerSegment, out string actionSegment)
        {
            pathTenant = null;
            if (segments.Length >= 2 && IsTenantAwareController(segments[1]))
            {
                pathTenant = segments[0];
                controllerSegment = segments[1];
                actionSegment = segments.Length >= 3 ? segments[2] : "Index";
                return;
            }

            controllerSegment = segments[0];
            actionSegment = segments.Length >= 2 ? segments[1] : "Index";
        }

        private static bool IsTenantAwareController(string segment)
        {
            return segment.Equals("Applications", StringComparison.OrdinalIgnoreCase) ||
                   segment.Equals("Positions", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveTenantSlug(string tenantSlug, string pathTenant)
        {
            return string.IsNullOrWhiteSpace(tenantSlug) ? pathTenant : tenantSlug;
        }

        private ActionResult BuildWhitelistedRedirect(string controllerSegment, string actionSegment, int segmentLength, string resolvedTenant, string queryString)
        {
            var query = HttpUtility.ParseQueryString(queryString ?? string.Empty);
            ActionResult applicationRedirect;

            if (controllerSegment.Equals("Applications", StringComparison.OrdinalIgnoreCase) &&
                TryBuildApplicationsRedirect(actionSegment, query, resolvedTenant, out applicationRedirect))
            {
                return applicationRedirect;
            }

            if (IsPositionsIndexRoute(controllerSegment, actionSegment, segmentLength))
            {
                return RedirectToAction("Index", "Positions", new { tenant = resolvedTenant });
            }

            return null;
        }

        private bool TryBuildApplicationsRedirect(string actionSegment, System.Collections.Specialized.NameValueCollection query, string resolvedTenant, out ActionResult redirectResult)
        {
            redirectResult = null;

            int positionId;
            if (!int.TryParse(query["positionId"], out positionId))
            {
                return false;
            }

            if (actionSegment.Equals("Questionnaire", StringComparison.OrdinalIgnoreCase))
            {
                redirectResult = RedirectToAction("Questionnaire", "Applications", new { tenant = resolvedTenant, positionId = positionId });
                return true;
            }

            if (actionSegment.Equals("Apply", StringComparison.OrdinalIgnoreCase))
            {
                redirectResult = RedirectToAction("Apply", "Applications", new { tenant = resolvedTenant, positionId = positionId });
                return true;
            }

            return false;
        }

        private static bool IsPositionsIndexRoute(string controllerSegment, string actionSegment, int segmentLength)
        {
            if (!controllerSegment.Equals("Positions", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return actionSegment.Equals("Index", StringComparison.OrdinalIgnoreCase) || segmentLength == 1;
        }

        // ── MFA Setup ──────────────────────────────────────────────────────────
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

        // ── MFA Verification ─────────────────────────────────────────────────────
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
            string username = Session["PendingMfaUsername"] as string ?? (User.Identity.IsAuthenticated ? User.Identity.Name : null);
            
            if (string.IsNullOrEmpty(username)) 
            {
                AuditSvc.LogAction("ANONYMOUS", "MFA_VERIFY_KICKBACK", "Account", "", "Session lost or identity missing during MFA POST");
                return RedirectToAction("Login");
            }

            var lowerUsername = username.ToLower();
            var user = _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
            
            if (user == null)
            {
                AuditSvc.LogAction(username, "MFA_VERIFY_KICKBACK", "Account", "", "User not found during MFA POST");
                return RedirectToAction("Login");
            }

            if (!user.IsTwoFactorEnabled)
            {
                AuditSvc.LogAction(username, "MFA_VERIFY_KICKBACK", "Account", user.Id.ToString(), "MFA was unexpectedly disabled during verification");
                return RedirectToAction("Login");
            }

            bool isValid = false;
            try 
            {
                isValid = SecuritySvc.ValidateTemporaryCode(user, code);
            }
            catch (Exception ex)
            {
                AuditSvc.LogAction(username, "MFA_VERIFY_ERROR", "Account", user.Id.ToString(), ex.Message);
            }

            if (isValid)
            {
                user.TwoFactorCode = null;
                _uow.Users.Update(user);
                _uow.Complete();

                Session.Remove("PendingMfaUsername");
                return CompleteLogin(user);
            }

            // Failure — render error
            AuditSvc.LogAction(username, "MFA_VERIFY_FAILED", "Account", user.Id.ToString(), "Invalid or expired MFA code entered");
            ModelState.AddModelError("", "Invalid or expired verification code.");
            ViewBag.MfaMethod = user.MfaMethod ?? "Email";
            ViewBag.EmailHint = MaskContactInfo(user.Email);
            return View();
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
            return name.Substring(0, 2) + "••••@ " + parts[1];
        }

        private ActionResult CompleteLogin(User user)
        {
            var userRole = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            var isSuperAdmin = !user.CompanyId.HasValue && (
                string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(userRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            );
            if (isSuperAdmin) { userRole = "SuperAdmin"; }

            var clientIP = Request.ServerVariables["HTTP_X_FORWARDED_FOR"] ?? Request.UserHostAddress;
            var uaHash = ComputeUaHash(Request.UserAgent);
            
            if (string.IsNullOrEmpty(user.AccessToken))
            {
                user.AccessToken = SecuritySvc.GenerateSecureToken();
                _uow.Users.Update(user);
                _uow.Complete();
            }

            var userData = string.Format("{0}|{1}|{2}|{3}", 
                userRole, 
                user.CompanyId, 
                user.AccessToken,
                uaHash);

            var ticket = new FormsAuthenticationTicket(
                1, 
                user.UserName, 
                DateTime.Now, 
                DateTime.Now.AddHours(8), 
                false, 
                userData);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,
                Secure   = Request.IsSecureConnection
            };
            Response.Cookies.Add(cookie);

            if (isSuperAdmin)
            {
                return RedirectToAction("Index", "Companies", new { tenant = (string)null });
            }
            else
            {
                string tenantSlug = null;
                if (user.CompanyId.HasValue)
                {
                    var company = _uow.Companies.Get(user.CompanyId.Value);
                    if (company != null) tenantSlug = company.Slug;
                }

                if (!string.IsNullOrEmpty(tenantSlug))
                {
                    // For regular admins, Dashboard is the logical landing page
                    if (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        return RedirectToAction("Index", "Dashboard", new { tenant = tenantSlug });
                    }
                    // For regular users (Clients), Positions is the landing page
                    return RedirectToAction("Index", "Positions", new { tenant = tenantSlug });
                }

                // Fallback for edge cases without company association
                return RedirectToAction("Index", "Positions");
            }
        }
    }
}
