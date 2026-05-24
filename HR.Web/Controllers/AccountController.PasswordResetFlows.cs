using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AccountController
    {
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
            DevDiagnostics.LogOneTimeCode("PASSWORD RESET TOKEN", user.Email, token);
            System.Diagnostics.Debug.WriteLine("=== PASSWORD RESET LINK ===");
            System.Diagnostics.Debug.WriteLine("Reset URL: " + resetUrl);
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
                SetPasswordResetSuccessMessage(ipMismatch);
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

        private void SetPasswordResetSuccessMessage(bool ipMismatch)
        {
            ViewBag.SuccessMessage = ipMismatch
                ? "Your password has been reset. NOTE: This reset was completed from a different location than where it was requested. If you did not initiate this reset, please contact your administrator immediately."
                : "Your password has been successfully reset. You can now login with your new password.";
        }
    }
}
