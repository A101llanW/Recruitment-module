using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AccountController
    {
        private sealed class LoginContextModel
        {
            public string Role { get; set; }
            public bool IsSuperAdmin { get; set; }
            public string TenantSlug { get; set; }
        }

        private ActionResult HandleChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username = User.Identity.Name;
            var user = FindUserByUsername(username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(model);
            }

            var isForcedChange = HasForcedPasswordChangeFlag();
            var currentPasswordResult = ValidateCurrentPasswordForChange(user, model, isForcedChange, username);
            if (currentPasswordResult != null)
            {
                return currentPasswordResult;
            }

            var newPasswordValidationMessage = ValidateNewPasswordForChange(user, model.NewPassword);
            if (newPasswordValidationMessage != null)
            {
                ModelState.AddModelError("", newPasswordValidationMessage);
                return View(model);
            }

            try
            {
                ApplyPasswordChange(user, model.NewPassword);
                AuditSvc.LogAction(
                    username,
                    "PASSWORD_CHANGED",
                    "Account",
                    user.Id.ToString(),
                    "Password successfully changed to meet security requirements");

                SetPasswordChangeSuccessMessage(isForcedChange);

                return CompleteLogin(user);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while changing your password. Please try again.");
                AuditSvc.LogAction(
                    username,
                    "PASSWORD_CHANGE_ERROR",
                    "Account",
                    user.Id.ToString(),
                    "Password change failed: " + ex.Message);
                return View(model);
            }
        }

        private void SetPasswordChangeSuccessMessage(bool isForcedChange)
        {
            TempData["SuccessMessage"] = isForcedChange
                ? "Your password has been successfully updated! You can now access the system with your new secure password."
                : "Your password has been successfully updated!";
        }

        private User FindUserByUsername(string username)
        {
            var lowerUsername = username.ToLower();
            return _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
        }

        private bool HasForcedPasswordChangeFlag()
        {
            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null)
            {
                return false;
            }

            var ticket = FormsAuthentication.Decrypt(authCookie.Value);
            return ticket != null && ticket.UserData.Contains("RequirePasswordChange");
        }

        private ActionResult ValidateCurrentPasswordForChange(User user, ChangePasswordViewModel model, bool isForcedChange, string username)
        {
            if (isForcedChange)
            {
                return null;
            }

            if (PasswordHelper.VerifyPassword(user.PasswordHash, model.CurrentPassword))
            {
                return null;
            }

            ModelState.AddModelError("", "Current password is incorrect.");
            AuditSvc.LogAction(
                username,
                "PASSWORD_CHANGE_FAILED",
                "Account",
                user.Id.ToString(),
                "Current password verification failed");
            return View(model);
        }

        private static string ValidateNewPasswordForChange(User user, string newPassword)
        {
            if (!PasswordHelper.IsPasswordStrong(newPassword))
            {
                return PasswordHelper.GetPasswordStrengthMessage();
            }

            if (PasswordHelper.VerifyPassword(user.PasswordHash, newPassword))
            {
                return "New password must be different from current password.";
            }

            return null;
        }

        private static void ApplyPasswordChange(User user, string newPassword)
        {
            user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            user.RequirePasswordChange = false;
            user.LastPasswordChange = DateTime.Now;
            user.PasswordChangeExpiry = null;
        }

        private ActionResult HandleProfileUpdate(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username = User.Identity.Name;
            var user = FindUserByUsername(username);
            if (user == null)
            {
                return HttpNotFound();
            }

            var oldValues = new { user.FirstName, user.LastName, user.Email, user.Phone };
            var previousEmail = user.Email;

            var emailResult = HandleProfileEmailChange(user, model.Email);
            if (emailResult != null)
            {
                return emailResult;
            }

            ApplyProfileUpdates(user, model);
            _uow.Users.Update(user);
            _uow.Complete();

            RenewAuthenticationCookie(username, user);
            SyncApplicantProfile(previousEmail, model);
            AuditSvc.LogUpdate(
                username,
                "Account",
                user.Id.ToString(),
                oldValues,
                new { model.FirstName, model.LastName, model.Email, model.Phone });

            TempData["SuccessMessage"] = "Your profile has been updated successfully!";
            return RedirectToAction("Profile");
        }

        private ActionResult HandleProfileEmailChange(User user, string newEmail)
        {
            if (user.Email == newEmail)
            {
                return null;
            }

            var existingUser = _uow.Context.Users.FirstOrDefault(u => u.Email == newEmail && u.Id != user.Id);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "This email address is already in use by another account.");
                return View("Profile");
            }

            user.AccessToken = SecuritySvc.GenerateSecureToken();
            user.IsEmailVerified = false;
            user.EmailVerificationCode = null;
            user.EmailVerificationExpiry = null;
            return null;
        }

        private static void ApplyProfileUpdates(User user, ProfileViewModel model)
        {
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;
            user.Phone = model.Phone;
        }

        private void RenewAuthenticationCookie(string username, User user)
        {
            var userRole = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            var uaHash = ComputeUaHash(Request.UserAgent);
            var userData = string.Format("{0}|{1}|{2}|{3}", userRole, user.CompanyId, user.AccessToken, uaHash);

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
                Secure = Request.IsSecureConnection
            };
            Response.Cookies.Add(cookie);
        }

        private void SyncApplicantProfile(string previousEmail, ProfileViewModel model)
        {
            var applicant = _uow.Context.Applicants.FirstOrDefault(a => a.Email == previousEmail);
            if (applicant == null && !string.Equals(previousEmail, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                applicant = _uow.Context.Applicants.FirstOrDefault(a => a.Email == model.Email);
            }

            if (applicant == null)
            {
                return;
            }

            applicant.FullName = string.Format("{0} {1}", model.FirstName, model.LastName);
            applicant.Email = model.Email;
            applicant.Phone = model.Phone;
            _uow.Applicants.Update(applicant);
            _uow.Complete();
        }

        private User ResolveCurrentUserFromIdentity(string username)
        {
            var lowerUsername = username.ToLower();
            var companyId = TryExtractCompanyIdFromIdentity();

            var scopedUser = FindScopedUser(lowerUsername, companyId);
            if (scopedUser != null)
            {
                return scopedUser;
            }

            return _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
        }

        private int? TryExtractCompanyIdFromIdentity()
        {
            var formsIdentity = GetFormsIdentityFromPrincipal();
            if (formsIdentity == null)
            {
                return null;
            }

            var props = formsIdentity.Ticket.UserData.Split('|');
            if (props.Length < 2)
            {
                return null;
            }

            return int.TryParse(props[1], out var companyId) ? companyId : (int?)null;
        }

        private FormsIdentity GetFormsIdentityFromPrincipal()
        {
            var formsIdentity = User.Identity as FormsIdentity;
            if (formsIdentity != null)
            {
                return formsIdentity;
            }

            if (User is RolePrincipal rolePrincipal)
            {
                return rolePrincipal.Identity as FormsIdentity;
            }

            return null;
        }

        private User FindScopedUser(string lowerUsername, int? companyId)
        {
            if (companyId.HasValue)
            {
                return _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername && u.CompanyId == companyId.Value);
            }

            return _uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername && u.CompanyId == null);
        }

        private ActionResult HandleVerifyEmailSubmit(string code)
        {
            var username = User.Identity.Name;
            var user = GetCurrentUserFromIdentity(username);
            if (user == null)
            {
                return HttpNotFound();
            }

            var verificationResult = ValidateEmailVerificationCode(user, code);
            if (verificationResult != null)
            {
                return verificationResult;
            }

            ApplySuccessfulEmailVerification(user);
            _uow.Users.Update(user);
            _uow.Complete();

            AuditSvc.LogAction(username, "EMAIL_VERIFIED", "Account", user.Id.ToString(), "Email verified via login screen");
            MarkApplicantEmailVerified(user.Email);
            return RedirectAfterEmailVerification(user, username);
        }

        private ActionResult ValidateEmailVerificationCode(User user, string code)
        {
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

            return null;
        }

        private static void ApplySuccessfulEmailVerification(User user)
        {
            user.IsEmailVerified = true;
            user.EmailVerificationCode = null;
            user.EmailVerificationExpiry = null;

            if (!user.IsTwoFactorEnabled)
            {
                user.IsTwoFactorEnabled = true;
                user.MfaMethod = "Email";
            }
        }

        private void MarkApplicantEmailVerified(string userEmail)
        {
            var applicant = _uow.Context.Applicants.FirstOrDefault(a => a.Email == userEmail);
            if (applicant == null)
            {
                return;
            }

            applicant.IsEmailVerified = true;
            _uow.Applicants.Update(applicant);
            _uow.Complete();
        }

        private ActionResult RedirectAfterEmailVerification(User user, string username)
        {
            var userRole = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            var isSuperAdmin = !user.CompanyId.HasValue &&
                (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(userRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase));

            if (isSuperAdmin || string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                Session.Remove("PendingMfaUsername");
                Session.Remove("ForcedMfaSetup");
                Session["MfaVerified"] = true;

                AuditSvc.LogAction(
                    username,
                    "LOGIN_MFA_BYPASSED",
                    "Account",
                    user.Id.ToString(),
                    true,
                    "MFA challenge seamlessly bypassed for first login after email verification");

                var tenantToken = RouteData.Values["tenant"] as string;
                if (isSuperAdmin)
                {
                    return RedirectToAction("Index", "Companies", new { tenant = (string)null });
                }

                return RedirectToAction("Index", "Positions", new { tenant = tenantToken });
            }

            var fallbackTenant = RouteData.Values["tenant"] as string;
            if (isSuperAdmin)
            {
                return RedirectToAction("Index", "Companies", new { tenant = (string)null });
            }

            return RedirectToAction("Index", "Positions", new { tenant = fallbackTenant });
        }

        private ActionResult HandleVerifyMfaSubmission(string code)
        {
            var username = Session["PendingMfaUsername"] as string ?? (User.Identity.IsAuthenticated ? User.Identity.Name : null);
            if (string.IsNullOrEmpty(username))
            {
                AuditSvc.LogAction("ANONYMOUS", "MFA_VERIFY_KICKBACK", "Account", "", "Session lost or identity missing during MFA POST");
                return RedirectToAction("Login");
            }

            var user = FindUserByUsername(username);
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

            var isValid = ValidateMfaCode(username, user, code);
            if (isValid)
            {
                user.TwoFactorCode = null;
                _uow.Users.Update(user);
                _uow.Complete();

                Session.Remove("PendingMfaUsername");
                return CompleteLogin(user);
            }

            AuditSvc.LogAction(username, "MFA_VERIFY_FAILED", "Account", user.Id.ToString(), "Invalid or expired MFA code entered");
            ModelState.AddModelError("", "Invalid or expired verification code.");
            ViewBag.MfaMethod = user.MfaMethod ?? "Email";
            ViewBag.EmailHint = MaskContactInfo(user.Email);
            return View("VerifyMFA");
        }

        private bool ValidateMfaCode(string username, User user, string code)
        {
            try
            {
                return SecuritySvc.ValidateTemporaryCode(user, code);
            }
            catch (Exception ex)
            {
                AuditSvc.LogAction(username, "MFA_VERIFY_ERROR", "Account", user.Id.ToString(), ex.Message);
                return false;
            }
        }

        private ActionResult CompleteUserLogin(User user)
        {
            var loginContext = BuildLoginContext(user);
            EnsureUserAccessToken(user);
            IssueLoginCookie(user, loginContext.Role);
            return BuildLoginRedirect(loginContext);
        }

        private LoginContextModel BuildLoginContext(User user)
        {
            var role = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            var isSuperAdmin = !user.CompanyId.HasValue &&
                (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase));
            if (isSuperAdmin)
            {
                role = "SuperAdmin";
            }

            return new LoginContextModel
            {
                Role = role,
                IsSuperAdmin = isSuperAdmin,
                TenantSlug = ResolveTenantSlugForLogin(user)
            };
        }

        private string ResolveTenantSlugForLogin(User user)
        {
            if (!user.CompanyId.HasValue)
            {
                return null;
            }

            var company = _uow.Companies.Get(user.CompanyId.Value);
            return company != null ? company.Slug : null;
        }

        private void EnsureUserAccessToken(User user)
        {
            if (!string.IsNullOrEmpty(user.AccessToken))
            {
                return;
            }

            user.AccessToken = SecuritySvc.GenerateSecureToken();
            _uow.Users.Update(user);
            _uow.Complete();
        }

        private void IssueLoginCookie(User user, string userRole)
        {
            var uaHash = ComputeUaHash(Request.UserAgent);
            var userData = string.Format("{0}|{1}|{2}|{3}", userRole, user.CompanyId, user.AccessToken, uaHash);

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
                Secure = Request.IsSecureConnection
            };
            Response.Cookies.Add(cookie);
        }

        private ActionResult BuildLoginRedirect(LoginContextModel context)
        {
            if (context.IsSuperAdmin)
            {
                return RedirectToAction("Index", "Companies", new { tenant = (string)null });
            }

            if (!string.IsNullOrEmpty(context.TenantSlug))
            {
                if (string.Equals(context.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Index", "Dashboard", new { tenant = context.TenantSlug });
                }

                return RedirectToAction("Index", "Positions", new { tenant = context.TenantSlug });
            }

            return RedirectToAction("Index", "Positions");
        }

        private async Task<ActionResult> HandleForgotPassword(ForgotPasswordViewModel model)
        {
            var clientIp = GetClientIp();
            if (IsForgotPasswordRateLimited(clientIp))
            {
                AuditSvc.LogAction(
                    "ANONYMOUS",
                    "FORGOT_PASSWORD_RATE_LIMITED",
                    "Account",
                    "",
                    string.Format("IP {0} exceeded forgot-password rate limit", clientIp));
                ViewBag.Message = "If an account with that email exists, a reset link has been sent.";
                return View("ForgotPassword");
            }

            RecordForgotPasswordAttempt(model, clientIp);
            if (!ModelState.IsValid)
            {
                return View("ForgotPassword", model);
            }

            try
            {
                var user = _uow.Context.Users.FirstOrDefault(u => u.Email == model.Email);
                if (user != null)
                {
                    await CreateAndSendPasswordResetToken(user, clientIp);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No user found with email: " + model.Email);
                    AuditSvc.LogAction("GUEST", "PASSWORD_RESET_ATTEMPT", "Account", "", null, null, false, "Email not found: " + model.Email);
                }

                ViewBag.SuccessMessage = "If an account with that email exists, a password reset link has been sent.";
                return View("ForgotPassword", model);
            }
            catch (Exception ex)
            {
                var errorMessage = BuildExceptionMessage(ex);
                AuditSvc.LogAction("SYSTEM", "PASSWORD_RESET_ERROR", "Account", "", "Password reset failed: " + errorMessage);
                var fullMessage = "An error occurred while processing your request: " + errorMessage;
                ModelState.AddModelError("", fullMessage);
                ViewBag.ErrorMessage = fullMessage;
                return View("ForgotPassword", model);
            }
        }

        private string GetClientIp()
        {
            return Request.ServerVariables["HTTP_X_FORWARDED_FOR"] ?? Request.UserHostAddress;
        }

        private bool IsForgotPasswordRateLimited(string clientIp)
        {
            const int maxForgotRequests = 5;
            const int forgotWindowMinutes = 10;
            var requestWindowStart = DateTime.Now.AddMinutes(-forgotWindowMinutes);
            var recentRequestCount = _uow.LoginAttempts.GetAll()
                .Count(a =>
                    a.IPAddress == clientIp &&
                    a.FailureReason == "FORGOT_PASSWORD_REQUEST" &&
                    a.AttemptTime > requestWindowStart);

            return recentRequestCount >= maxForgotRequests;
        }

        private void RecordForgotPasswordAttempt(ForgotPasswordViewModel model, string clientIp)
        {
            _uow.LoginAttempts.Add(
                new LoginAttempt
                {
                    Username = model.Email ?? "unknown",
                    IPAddress = clientIp,
                    AttemptTime = DateTime.Now,
                    WasSuccessful = false,
                    FailureReason = "FORGOT_PASSWORD_REQUEST"
                });
            _uow.Complete();
        }

        private async Task CreateAndSendPasswordResetToken(User user, string requestingIp)
        {
            var token = SecuritySvc.GenerateSecureToken();
            var expiryDate = DateTime.UtcNow.AddHours(24);

            InvalidateExistingPasswordResetTokens(user.Id);
            _uow.PasswordResets.Add(
                new PasswordReset
                {
                    UserId = user.Id,
                    Token = token,
                    ExpiryDate = expiryDate,
                    CreatedDate = DateTime.UtcNow,
                    RequestingIP = requestingIp
                });
            _uow.Complete();

            var tenantToken = RouteData.Values["tenant"] as string;
            var resetUrl = Url.Action("ResetPassword", "Account", new { tenant = tenantToken, token = token }, Request.Url.Scheme);
            System.Diagnostics.Debug.WriteLine("=== PASSWORD RESET LINK ===");
            System.Diagnostics.Debug.WriteLine("Reset URL: " + resetUrl);
            System.Diagnostics.Debug.WriteLine("Token: " + token);
            System.Diagnostics.Debug.WriteLine("User Email: " + user.Email);
            System.Diagnostics.Debug.WriteLine("========================");

            if (EmailSvc != null)
            {
                await EmailSvc.SendPasswordResetEmailAsync(user.Email, resetUrl);
            }

            AuditSvc.LogAction(
                user.UserName,
                "PASSWORD_RESET_REQUEST",
                "Account",
                user.Id.ToString(),
                null,
                null,
                true,
                string.Format("Reset link sent to {0} from IP {1}", user.Email, requestingIp));
        }

        private void InvalidateExistingPasswordResetTokens(int userId)
        {
            var existingTokens = _uow.PasswordResets.GetAll().Where(t => t.UserId == userId && !t.IsUsed);
            foreach (var existingToken in existingTokens)
            {
                existingToken.IsUsed = true;
            }
        }

        private static string BuildExceptionMessage(Exception ex)
        {
            return ex.InnerException != null
                ? ex.Message + " Inner Error: " + ex.InnerException.Message
                : ex.Message;
        }

        private ActionResult HandleResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("ResetPassword", model);
            }

            try
            {
                var passwordReset = FindValidPasswordReset(model.Token);
                if (passwordReset == null)
                {
                    ViewBag.ErrorMessage = "This password reset link is invalid or has expired.";
                    return View("ResetPassword", model);
                }

                var user = _uow.Users.Get(passwordReset.UserId);
                if (user == null)
                {
                    ViewBag.ErrorMessage = "User not found.";
                    return View("ResetPassword", model);
                }

                if (!PasswordHelper.IsPasswordStrong(model.NewPassword))
                {
                    ModelState.AddModelError("", PasswordHelper.GetPasswordStrengthMessage());
                    return View("ResetPassword", model);
                }

                var ipMismatch = CompletePasswordReset(passwordReset, user, model.NewPassword);
                LogPasswordResetSuccess(user, passwordReset, ipMismatch);
                SetPasswordResetSuccessMessage(passwordReset, ipMismatch);
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                AuditSvc.LogAction("SYSTEM", "PASSWORD_RESET_ERROR", "Account", "", "Password reset completion failed: " + ex.Message);
                ModelState.AddModelError("", "An error occurred while resetting your password. Please try again.");
                return View("ResetPassword", model);
            }
        }

        private PasswordReset FindValidPasswordReset(string token)
        {
            return _uow.PasswordResets.GetAll()
                .FirstOrDefault(t => t.Token == token && !t.IsUsed && t.ExpiryDate > DateTime.UtcNow);
        }

        private bool CompletePasswordReset(PasswordReset passwordReset, User user, string newPassword)
        {
            var completingIp = GetClientIp();
            passwordReset.CompletedIP = completingIp;
            var ipMismatch = !string.IsNullOrEmpty(passwordReset.RequestingIP) && passwordReset.RequestingIP != completingIp;

            user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            user.RequirePasswordChange = false;
            user.LastPasswordChange = DateTime.UtcNow;
            user.PasswordChangeExpiry = null;
            passwordReset.IsUsed = true;
            _uow.Complete();

            return ipMismatch;
        }

        private void LogPasswordResetSuccess(User user, PasswordReset passwordReset, bool ipMismatch)
        {
            var resetNote = ipMismatch
                ? string.Format(
                    "Password reset completed. IP MISMATCH: requested from {0}, completed from {1}",
                    passwordReset.RequestingIP,
                    passwordReset.CompletedIP)
                : "Password was successfully reset";

            AuditSvc.LogAction(user.UserName, "PASSWORD_RESET_SUCCESS", "Account", user.Id.ToString(), resetNote);
        }

        private void SetPasswordResetSuccessMessage(PasswordReset passwordReset, bool ipMismatch)
        {
            ViewBag.SuccessMessage = ipMismatch
                ? "Your password has been reset. NOTE: This reset was completed from a different location than where it was requested. If you did not initiate this reset, please contact your administrator immediately."
                : "Your password has been successfully reset. You can now login with your new password.";
        }
    }
}
