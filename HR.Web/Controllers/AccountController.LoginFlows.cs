using System;
using System.Data.Entity;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    public partial class AccountController
    {
        private sealed class LoginRequestModel
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Captcha { get; set; }
            public string ReturnUrl { get; set; }
            public string UrlTenantToken { get; set; }
            public string ClientIp { get; set; }
            public bool IsGlobalSuperAdmin { get; set; }
            public bool IsEmailLogin { get; set; }
            public string LowerUsername { get; set; }
        }

        private sealed class LoginCandidateModel
        {
            public List<User> Candidates { get; set; }
            public User PrimaryUser { get; set; }
            public int? EffectiveCompanyId { get; set; }
        }

        private sealed class LoginRoutingContextModel
        {
            public string Role { get; set; }
            public bool IsSuperAdmin { get; set; }
            public string TenantSlug { get; set; }
        }

        private ActionResult HandleLoginPost(string username, string password, string captcha, string role, string returnUrl)
        {
            _ = role;

            try
            {
                var request = BuildLoginRequest(username, password, captcha, returnUrl);
                var targetCompanyId = ResolveTargetCompanyId(request.UrlTenantToken);

                var captchaFailure = ValidateSubmittedCaptcha(request);
                if (captchaFailure != null)
                {
                    return captchaFailure;
                }

                var inputFailure = ValidateLoginInputs(request, targetCompanyId);
                if (inputFailure != null)
                {
                    return inputFailure;
                }

                var rateLimitFailure = ValidateIpRateLimit(request.ClientIp);
                if (rateLimitFailure != null)
                {
                    return rateLimitFailure;
                }

                var candidates = DiscoverLoginCandidates(request, targetCompanyId);

                var lockoutFailure = ValidateAccountLockout(request, candidates.EffectiveCompanyId);
                if (lockoutFailure != null)
                {
                    return lockoutFailure;
                }

                var disambiguationFailure = HandleLoginDisambiguation(request, candidates.Candidates, targetCompanyId);
                if (disambiguationFailure != null)
                {
                    return disambiguationFailure;
                }

                return FinalizeLogin(request, candidates.PrimaryUser, targetCompanyId);
            }
            catch (Exception ex)
            {
                AuditSvc.LogAction(
                    username,
                    "LOGIN_CRASH",
                    "Account",
                    "",
                    wasSuccessful: false,
                    errorMessage: "CRASH: " + ex.Message + " | Stack: " + ex.StackTrace);
                ModelState.AddModelError("", "A system error occurred. Our team has been notified.");
                return View();
            }
        }

        private LoginRequestModel BuildLoginRequest(string username, string password, string captcha, string returnUrl)
        {
            var urlTenantToken = RouteData.Values["tenant"] as string;

            return new LoginRequestModel
            {
                Username = username,
                Password = password,
                Captcha = captcha,
                ReturnUrl = returnUrl,
                UrlTenantToken = urlTenantToken,
                ClientIp = Request.UserHostAddress,
                IsGlobalSuperAdmin = !string.IsNullOrEmpty(username) &&
                                     string.Equals(username, "SuperAdmin", StringComparison.OrdinalIgnoreCase) &&
                                     string.IsNullOrEmpty(urlTenantToken)
            };
        }

        private int? ResolveTargetCompanyId(string urlTenantToken)
        {
            if (string.IsNullOrEmpty(urlTenantToken))
            {
                return null;
            }

            var targetCompany = _uow.Companies.GetAll().FirstOrDefault(c => c.Slug == urlTenantToken);
            return targetCompany != null ? (int?)targetCompany.Id : null;
        }

        private ActionResult ValidateSubmittedCaptcha(LoginRequestModel request)
        {
            var sessionCaptchaText = Session["CaptchaText"] as string;
            var sessionExpiry = Session["CaptchaExpiry"] as DateTime?;
            var sessionCaptchaId = Session["CaptchaId"] as string;

            if (string.IsNullOrEmpty(sessionCaptchaText) || !sessionExpiry.HasValue || string.IsNullOrEmpty(sessionCaptchaId))
            {
                return BuildCaptchaFailureResult("CAPTCHA session expired. Please try again.", request.ReturnUrl);
            }

            if (DateTime.UtcNow > sessionExpiry.Value)
            {
                ClearCaptchaSession();
                return BuildCaptchaFailureResult("CAPTCHA expired. Please try again.", request.ReturnUrl);
            }

            if (string.IsNullOrEmpty(request.Captcha) || !string.Equals(request.Captcha, sessionCaptchaText, StringComparison.Ordinal))
            {
                return BuildCaptchaFailureResult("Invalid security code. Please try again.", request.ReturnUrl);
            }

            ClearCaptchaSession();
            return null;
        }

        private void ClearCaptchaSession()
        {
            Session.Remove("CaptchaText");
            Session.Remove("CaptchaExpiry");
            Session.Remove("CaptchaId");
        }

        private ActionResult BuildCaptchaFailureResult(string message, string returnUrl)
        {
            ModelState.AddModelError("", message);
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(RouteData.Values["tenant"] as string);
            return View();
        }

        private ActionResult ValidateLoginInputs(LoginRequestModel request, int? targetCompanyId)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return BuildMissingCredentialResult("Username is required.", request, targetCompanyId, "Username required");
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BuildMissingCredentialResult("Password is required.", request, targetCompanyId, "Password required");
            }

            request.Username = request.Username.Trim();
            request.LowerUsername = request.Username.ToLower();
            request.IsEmailLogin = request.Username.Contains("@");
            return null;
        }

        private ActionResult BuildMissingCredentialResult(string message, LoginRequestModel request, int? targetCompanyId, string failureReason)
        {
            ModelState.AddModelError("", message);
            SecuritySvc.RecordLoginAttempt(request.Username, request.ClientIp, false, targetCompanyId, failureReason);
            AuditSvc.LogLogin(request.Username, false, failureReason);
            ViewBag.ReturnUrl = request.ReturnUrl;
            ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(request.UrlTenantToken);
            return View();
        }

        private ActionResult ValidateIpRateLimit(string clientIp)
        {
            const int maxIpFailures = 20;
            const int ipWindowMinutes = 15;
            var windowStart = DateTime.Now.AddMinutes(-ipWindowMinutes);

            var ipFailureCount = _uow.LoginAttempts.GetAll()
                .Count(a => a.IPAddress == clientIp && !a.WasSuccessful && a.AttemptTime > windowStart);

            if (ipFailureCount < maxIpFailures)
            {
                return null;
            }

            AuditSvc.LogAction(
                "ANONYMOUS",
                "IP_RATE_LIMITED",
                "Account",
                "",
                string.Format("IP {0} blocked after {1} failed attempts in {2} minutes", clientIp, ipFailureCount, ipWindowMinutes));
            ModelState.AddModelError("", "Too many failed login attempts from your location. Please wait 15 minutes before trying again.");
            ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(RouteData.Values["tenant"] as string);
            return View();
        }

        private LoginCandidateModel DiscoverLoginCandidates(LoginRequestModel request, int? targetCompanyId)
        {
            var discoveryQuery = _uow.Context.Users.Include(u => u.Company).AsQueryable();
            if (targetCompanyId.HasValue)
            {
                discoveryQuery = discoveryQuery.Where(u => u.CompanyId == targetCompanyId.Value);
            }

            var candidates = request.IsEmailLogin
                ? discoveryQuery.Where(u => u.Email != null && u.Email.ToLower() == request.LowerUsername).ToList()
                : discoveryQuery.Where(u => u.UserName != null && u.UserName.ToLower() == request.LowerUsername).ToList();

            return new LoginCandidateModel
            {
                Candidates = candidates,
                PrimaryUser = candidates.FirstOrDefault(),
                EffectiveCompanyId = targetCompanyId ?? (candidates.Count == 1 ? candidates[0].CompanyId : null)
            };
        }

        private ActionResult ValidateAccountLockout(LoginRequestModel request, int? effectiveCompanyId)
        {
            if (request.IsGlobalSuperAdmin)
            {
                return null;
            }

            if (!SecuritySvc.IsAccountLocked(request.Username, effectiveCompanyId))
            {
                return null;
            }

            var lockoutEndTime = SecuritySvc.GetLockoutEndTime(request.Username, effectiveCompanyId);
            var remainingTime = lockoutEndTime.HasValue ? lockoutEndTime.Value - DateTime.Now : TimeSpan.Zero;

            ModelState.AddModelError("", string.Format("Account is locked. Please try again in {0} minutes.", remainingTime.Minutes));
            SecuritySvc.RecordLoginAttempt(request.Username, request.ClientIp, false, effectiveCompanyId, "Account locked");
            AuditSvc.LogLogin(request.Username, false, string.Format("Account locked. Try again in {0} minutes", remainingTime.Minutes));
            ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(request.UrlTenantToken);
            return View();
        }

        private ActionResult HandleLoginDisambiguation(LoginRequestModel request, List<User> candidates, int? targetCompanyId)
        {
            if (!request.IsEmailLogin && candidates.Count > 1 && !targetCompanyId.HasValue)
            {
                ModelState.AddModelError("", "This username is used by multiple companies. Please use your email address to help us find the right account.");
                ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(request.UrlTenantToken);
                return View();
            }

            if (candidates.Count > 1 && !targetCompanyId.HasValue)
            {
                ViewBag.MultiCandidates = candidates;
                ModelState.AddModelError("", "We found multiple accounts for this email. Please select the correct portal below.");
                ViewBag.ReturnUrl = request.ReturnUrl;
                ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(request.UrlTenantToken);
                return View();
            }

            return null;
        }

        private ActionResult FinalizeLogin(LoginRequestModel request, User user, int? targetCompanyId)
        {
            if (user == null)
            {
                return BuildIdentifierNotFoundFailure(request, targetCompanyId);
            }

            var passwordFailure = ValidateUserPassword(request, user);
            if (passwordFailure != null)
            {
                return passwordFailure;
            }

            var companyAccessFailure = ValidateCompanyPortalAccessForLogin(user);
            if (companyAccessFailure != null)
            {
                return companyAccessFailure;
            }

            return CompleteLoginForValidatedUser(request, user);
        }

        private ActionResult ValidateCompanyPortalAccessForLogin(User user)
        {
            if (!user.CompanyId.HasValue)
            {
                return null;
            }

            var company = _uow.Companies.Get(user.CompanyId.Value);
            if (company == null)
            {
                ModelState.AddModelError("", "Your company record could not be found. Contact support.");
                ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(RouteData.Values["tenant"] as string);
                return View();
            }

            if (!company.IsActive)
            {
                ModelState.AddModelError("", "Your company portal is inactive. Contact your system administrator.");
                ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(RouteData.Values["tenant"] as string);
                return View();
            }

            var tenantService = new TenantService(_uow);
            if (!tenantService.IsCompanyLicenseActive(company.Id))
            {
                return RedirectToAction("LicenseExpired", "Account", new { tenant = company.Slug });
            }

            return null;
        }

        private ActionResult BuildIdentifierNotFoundFailure(LoginRequestModel request, int? targetCompanyId)
        {
            ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(request.UrlTenantToken);
            ViewBag.ReturnUrl = request.ReturnUrl;

            if (targetCompanyId.HasValue)
            {
                var targetCompany = _uow.Companies.Get(targetCompanyId.Value);
                var targetCompanyName = targetCompany != null ? targetCompany.Name : "this company";
                var accountsElsewhere = FindAccountsInOtherCompanies(request, targetCompanyId.Value);

                if (accountsElsewhere.Count > 0)
                {
                    ViewBag.MultiCandidates = accountsElsewhere;
                    var identifierLabel = request.IsEmailLogin ? "email" : "username";

                    if (accountsElsewhere.Count == 1)
                    {
                        var otherCompanyName = accountsElsewhere[0].Company != null
                            ? accountsElsewhere[0].Company.Name
                            : "another company";
                        ModelState.AddModelError(
                            "",
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "No account with this {0} is registered for {1}. Your {0} is associated with {2}. Sign in using that company's portal below.",
                                identifierLabel,
                                targetCompanyName,
                                otherCompanyName));
                    }
                    else
                    {
                        ModelState.AddModelError(
                            "",
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "No account with this {0} is registered for {1}. Matching accounts were found at other companies—select the correct portal below.",
                                identifierLabel,
                                targetCompanyName));
                    }

                    SecuritySvc.RecordLoginAttempt(request.Username, request.ClientIp, false, targetCompanyId, "Identifier not found in tenant");
                    AuditSvc.LogLogin(request.Username, false, "Identifier not in tenant, found elsewhere: " + request.Username);
                    return View();
                }
            }

            ModelState.AddModelError("", "Invalid username or password.");
            SecuritySvc.RecordLoginAttempt(request.Username, request.ClientIp, false, targetCompanyId, "Identifier not found");
            AuditSvc.LogLogin(request.Username, false, "Invalid identifier: " + request.Username);
            return View();
        }

        private List<User> FindAccountsInOtherCompanies(LoginRequestModel request, int targetCompanyId)
        {
            var elsewhereQuery = _uow.Context.Users
                .Include(u => u.Company)
                .Where(u => u.CompanyId != targetCompanyId);

            if (request.IsEmailLogin)
            {
                return elsewhereQuery
                    .Where(u => u.Email != null && u.Email.ToLower() == request.LowerUsername)
                    .ToList();
            }

            return elsewhereQuery
                .Where(u => u.UserName != null && u.UserName.ToLower() == request.LowerUsername)
                .ToList();
        }

        private ActionResult ValidateUserPassword(LoginRequestModel request, User user)
        {
            if (IsPasswordValid(user, request.Password))
            {
                return null;
            }

            var remainingAttempts = SecuritySvc.GetRemainingAttempts(request.Username, user.CompanyId);
            var warningMessage = BuildInvalidPasswordMessage(remainingAttempts);

            if (!request.IsGlobalSuperAdmin)
            {
                SecuritySvc.RecordLoginAttempt(request.Username, request.ClientIp, false, user.CompanyId, "Invalid password");
            }

            ModelState.AddModelError("", warningMessage);
            AuditSvc.LogLogin(request.Username, false, "Invalid password");
            ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(request.UrlTenantToken);
            return View();
        }

        private static bool IsPasswordValid(User user, string password)
        {
            return !string.IsNullOrEmpty(user.PasswordHash) && PasswordHelper.VerifyPassword(user.PasswordHash, password);
        }

        private static string BuildInvalidPasswordMessage(int remainingAttempts)
        {
            return remainingAttempts > 1
                ? string.Format("Invalid username or password. {0} attempts remaining.", remainingAttempts)
                : string.Format("Invalid username or password. {0} attempt remaining before account lockout.", remainingAttempts);
        }

        private ActionResult CompleteLoginForValidatedUser(LoginRequestModel request, User user)
        {
            var relationship = LegalPolicyHelper.ResolveUserLegalRelationship(user);
            if (!LegalPolicyHelper.UserMeetsCurrentPolicyVersions(user, relationship))
            {
                StorePendingLegalLogin(request, user);
                PopulateLegalConsentModalViewBag(request, user);
                ConfigureLoginPortalViewBag(request);
                ViewBag.ReturnUrl = request.ReturnUrl;
                return View("Login");
            }

            PersistUserLegalAcceptance(user);
            return FinalizeAuthenticatedSessionTail(request, user);
        }

        private ActionResult FinalizeAuthenticatedSessionTail(LoginRequestModel request, User user)
        {
            var loginContext = BuildLoginRoutingContext(user);
            RecordSuccessfulLoginState(request, user);
            EnsureLoginAccessToken(user, request.Username);
            StorePendingLoginContinuation(request, user);

            var emailVerificationRedirect = HandleUnverifiedEmailRedirect(request.Username, user, loginContext.TenantSlug);
            if (emailVerificationRedirect != null)
            {
                IssueLoginCookie(user, loginContext.Role);
                AuditSvc.LogAction(request.Username, "LOGIN_COOKIE_SET", "Account", user.Id.ToString(), true, "Auth cookie added for email verification");
                return emailVerificationRedirect;
            }

            if (RequiresPrivilegedMfa(loginContext))
            {
                return HandlePrivilegedMfaRedirect(request.Username, user, loginContext.TenantSlug);
            }

            IssueLoginCookie(user, loginContext.Role);
            AuditSvc.LogAction(request.Username, "LOGIN_COOKIE_SET", "Account", user.Id.ToString(), true, "Auth cookie added to response");
            return HandleStandardUserRedirect(request, user, loginContext.TenantSlug);
        }

        private void StorePendingLegalLogin(LoginRequestModel request, User user)
        {
            Session[LegalConsentSession.PendingUserIdSession] = user.Id;
            Session[LegalConsentSession.PendingStartedTicksSession] = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            Session[LegalConsentSession.PendingUsernameSession] = user.UserName;
            if (user.CompanyId.HasValue)
            {
                Session[LegalConsentSession.PendingCompanyIdSession] = user.CompanyId.Value;
            }
            else
            {
                Session.Remove(LegalConsentSession.PendingCompanyIdSession);
            }

            if (!string.IsNullOrWhiteSpace(request.ReturnUrl))
            {
                Session[LegalConsentSession.PendingReturnUrlSession] = request.ReturnUrl;
            }
            else
            {
                Session.Remove(LegalConsentSession.PendingReturnUrlSession);
            }
        }

        private void StorePendingLoginContinuation(LoginRequestModel request, User user)
        {
            Session[LegalConsentSession.PendingUsernameSession] = user.UserName;
            if (user.CompanyId.HasValue)
            {
                Session[LegalConsentSession.PendingCompanyIdSession] = user.CompanyId.Value;
            }
            else
            {
                Session.Remove(LegalConsentSession.PendingCompanyIdSession);
            }
        }

        private LoginRequestModel BuildPendingCompletionLoginRequest(User user)
        {
            var urlTenantToken = RouteData.Values["tenant"] as string;
            var returnUrl = Session[LegalConsentSession.PendingReturnUrlSession] as string;
            return new LoginRequestModel
            {
                Username = user.UserName,
                Password = null,
                Captcha = null,
                ReturnUrl = returnUrl,
                UrlTenantToken = urlTenantToken,
                ClientIp = Request.UserHostAddress,
                IsGlobalSuperAdmin = false,
                IsEmailLogin = false,
                LowerUsername = null
            };
        }

        private void PopulateLegalConsentModalViewBag(LoginRequestModel request, User user)
        {
            var tenant = request != null ? request.UrlTenantToken : null;
            ViewBag.ShowLegalConsentModal = true;
            ViewBag.LegalTermsUrl = Url.Action("Terms", "Home", new { tenant });
            ViewBag.LegalPrivacyUrl = Url.Action("Privacy", "Home", new { tenant });
            ViewBag.LegalConsentDisplayName = string.Format(CultureInfo.InvariantCulture, "{0} {1}", user.FirstName, user.LastName).Trim();
        }

        private ActionResult HandleConfirmLegalConsent(bool acceptLegalTerms)
        {
            ConfigureLoginPortalViewBag(new LoginRequestModel
            {
                UrlTenantToken = RouteData.Values["tenant"] as string,
            });

            var userId = LegalConsentSession.TryReadUserId(Session);
            if (!userId.HasValue || !LegalConsentSession.IsFresh(Session))
            {
                LegalConsentSession.Clear(Session);
                TempData["ErrorMessage"] = "Your sign-in confirmation expired. Please sign in again.";
                return RedirectToAction("Login", new { tenant = RouteData.Values["tenant"] as string });
            }

            var user = _uow.Users.Get(userId.Value);
            if (user == null)
            {
                LegalConsentSession.Clear(Session);
                TempData["ErrorMessage"] = "Your sign-in confirmation is no longer valid. Please sign in again.";
                return RedirectToAction("Login", new { tenant = RouteData.Values["tenant"] as string });
            }

            var relationship = LegalPolicyHelper.ResolveUserLegalRelationship(user);
            var resumeRequest = BuildPendingCompletionLoginRequest(user);

            if (!acceptLegalTerms)
            {
                ModelState.AddModelError("", "Please accept the Terms & Conditions and Privacy Policy to continue.");
                PopulateLegalConsentModalViewBag(resumeRequest, user);
                ViewBag.ReturnUrl = resumeRequest.ReturnUrl;
                return View("Login");
            }

            PersistUserLegalAcceptance(user);
            LegalConsentSession.Clear(Session);
            return FinalizeAuthenticatedSessionTail(resumeRequest, user);
        }

        private LoginRoutingContextModel BuildLoginRoutingContext(User user)
        {
            var userRole = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            var isSuperAdmin = !user.CompanyId.HasValue &&
                               (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(userRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase));

            if (isSuperAdmin)
            {
                userRole = "SuperAdmin";
            }

            return new LoginRoutingContextModel
            {
                Role = userRole,
                IsSuperAdmin = isSuperAdmin,
                TenantSlug = ResolveActiveTenantSlug(user, isSuperAdmin)
            };
        }

        private string ResolveActiveTenantSlug(User user, bool isSuperAdmin)
        {
            if (isSuperAdmin || !user.CompanyId.HasValue)
            {
                return null;
            }

            var company = _uow.Companies.Get(user.CompanyId.Value);
            return company != null && company.IsActive ? company.Slug : null;
        }

        private void RecordSuccessfulLoginState(LoginRequestModel request, User user)
        {
            AuditSvc.LogAction(request.Username, "LOGIN_START_AUTH", "Account", user.Id.ToString(), true, "Credentials valid, processing session");
            SecuritySvc.RecordLoginAttempt(request.Username, request.ClientIp, true, user.CompanyId);
            SecuritySvc.ClearFailedAttempts(request.Username, user.CompanyId);
            AuditSvc.LogLogin(request.Username, true);
        }

        private void EnsureLoginAccessToken(User user, string username)
        {
            if (!string.IsNullOrEmpty(user.AccessToken))
            {
                return;
            }

            user.AccessToken = SecuritySvc.GenerateSecureToken();
            _uow.Users.Update(user);
            _uow.Complete();
            AuditSvc.LogAction(username, "LOGIN_TOKEN_GEN", "Account", user.Id.ToString(), true, "New AccessToken generated");
        }

        private ActionResult HandleUnverifiedEmailRedirect(string username, User user, string tenantSlug)
        {
            if (user.IsEmailVerified)
            {
                return null;
            }

            AuditSvc.LogAction(username, "LOGIN_REDIRECT_EMAIL_VERIFY", "Account", user.Id.ToString(), true, "Redirecting to email verification");
            var otpCode = GenerateAndStoreEmailVerificationCode(user);
            QueueEmailVerificationDelivery(user.Email, otpCode);
            return RedirectToAction("VerifyEmail", "Account", new { tenant = tenantSlug });
        }

        private string GenerateAndStoreEmailVerificationCode(User user)
        {
            var otpCode = SecuritySvc.GenerateTemporaryCode();
            user.EmailVerificationCode = otpCode;
            user.EmailVerificationExpiry = DateTime.Now.AddMinutes(15);
            _uow.Users.Update(user);
            _uow.Complete();
            return otpCode;
        }

        private void QueueEmailVerificationDelivery(string userEmail, string securityToken)
        {
            try
            {
                EmailSvc.SendEmailVerificationOtpAsync(userEmail, securityToken).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("--- [EMAIL VERIFICATION ERROR] Failed to send: " + ex.Message);
                System.Diagnostics.Trace.WriteLine("--- [EMAIL VERIFICATION ERROR] Failed to send: " + ex.Message);
            }
        }

        private static bool RequiresPrivilegedMfa(LoginRoutingContextModel loginContext)
        {
            return loginContext.IsSuperAdmin || string.Equals(loginContext.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private ActionResult HandlePrivilegedMfaRedirect(string username, User user, string tenantSlug)
        {
            if (!user.IsTwoFactorEnabled)
            {
                Session["ForcedMfaSetup"] = user.UserName;
                return RedirectToAction("SetupMFA", "Account", new { tenant = tenantSlug });
            }

            AuditSvc.LogAction(username, "LOGIN_REDIRECT_MFA", "Account", user.Id.ToString(), true, "Redirecting to MFA challenge");
            Session["PendingMfaUsername"] = user.UserName;
            if (user.CompanyId.HasValue)
            {
                Session[LegalConsentSession.PendingCompanyIdSession] = user.CompanyId.Value;
            }

            if (UsesEmailMfa(user) || string.IsNullOrWhiteSpace(user.MfaMethod))
            {
                SendMfaCode(user, "Email");
            }

            return RedirectToAction("VerifyMFA", "Account", new { tenant = tenantSlug });
        }

        private ActionResult HandleStandardUserRedirect(LoginRequestModel request, User user, string tenantSlug)
        {
            if (string.IsNullOrEmpty(tenantSlug))
            {
                ModelState.AddModelError("", "Your account is not associated with an active company.");
                ViewBag.IsTenantCompanyPortal = !string.IsNullOrEmpty(request.UrlTenantToken);
                return View();
            }

            AddPreferredTenantCookie(tenantSlug);

            if (IsTenantMismatch(request.UrlTenantToken, tenantSlug))
            {
                AuditSvc.LogAction(request.Username, "LOGIN_REDIRECT_TENANT", "Account", user.Id.ToString(), true, "Redirecting to branded portal: " + tenantSlug);
                return RedirectToAction("Index", "Positions", new { tenant = tenantSlug });
            }

            var safeReturnResult = BuildSafeReturnRedirect(request.ReturnUrl, tenantSlug);
            if (safeReturnResult != null)
            {
                AuditSvc.LogAction(request.Username, "LOGIN_REDIRECT_RETURNURL", "Account", user.Id.ToString(), true, "Redirecting to validated return target");
                return safeReturnResult;
            }

            AuditSvc.LogAction(request.Username, "LOGIN_REDIRECT_DEFAULT", "Account", user.Id.ToString(), true, "Redirecting to default dashboard for " + tenantSlug);
            return RedirectToAction("Index", "Positions", new { tenant = tenantSlug });
        }

        private void ConfigureLoginPortalViewBag(LoginRequestModel request)
        {
            ViewBag.IsTenantCompanyPortal = request != null && !string.IsNullOrEmpty(request.UrlTenantToken);
        }

        private void PersistUserLegalAcceptance(User user)
        {
            var relationship = LegalPolicyHelper.ResolveUserLegalRelationship(user);
            var expectedPrivacy = LegalPolicyHelper.GetPrivacyVersion(relationship);
            var expectedTerms = LegalPolicyHelper.GetTermsVersion(relationship);
            var requiresPrivacyUpdate = !user.PrivacyAcceptedAt.HasValue ||
                                        !string.Equals(user.PrivacyVersion, expectedPrivacy, StringComparison.Ordinal);
            var requiresTermsUpdate = !user.TermsAcceptedAt.HasValue ||
                                      !string.Equals(user.TermsVersion, expectedTerms, StringComparison.Ordinal);
            if (!requiresPrivacyUpdate && !requiresTermsUpdate)
            {
                return;
            }

            LegalPolicyHelper.ApplyUserAcceptance(user, DateTime.UtcNow, relationship);
            _uow.Users.Update(user);
            _uow.Complete();
        }

        private void AddPreferredTenantCookie(string tenantSlug)
        {
            var prefCookie = new HttpCookie("PreferredTenant", tenantSlug)
            {
                Expires = DateTime.Now.AddDays(30),
                Path = "/"
            };
            Response.Cookies.Add(prefCookie);
        }

        private static bool IsTenantMismatch(string urlTenantToken, string correctTenantSlug)
        {
            return string.IsNullOrEmpty(urlTenantToken) ||
                   !string.Equals(urlTenantToken, correctTenantSlug, StringComparison.OrdinalIgnoreCase);
        }
    }
}
