using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AccountController
    {
        private sealed class RegistrationValidationError
        {
            public string Key { get; set; }
            public string Message { get; set; }
        }

        private ActionResult HandleRegisterGet(int? companyId, bool isSuperAdmin, Uri returnUri)
        {
            ViewBag.IsSuperAdmin = isSuperAdmin;

            var viewModel = CreateRegisterViewModel(companyId, isSuperAdmin);
            ApplyRegisterApplicationContext(viewModel, companyId, returnUri);
            return View(viewModel);
        }

        private RegisterViewModel CreateRegisterViewModel(int? companyId, bool isSuperAdmin)
        {
            return new RegisterViewModel
            {
                Companies = isSuperAdmin ? GetActiveCompanies() : new List<Company>(),
                CompanyId = companyId
            };
        }

        private List<Company> GetActiveCompanies()
        {
            return _uow.Companies.GetAll().Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
        }

        private void ApplyRegisterApplicationContext(RegisterViewModel viewModel, int? companyId, Uri returnUri)
        {
            if (TempData["ApplicationMessage"] != null)
            {
                ViewBag.ApplicationMessage = TempData["ApplicationMessage"].ToString();
            }

            Uri effectiveReturnUri = returnUri;
            if (effectiveReturnUri == null && TempData["ReturnUrl"] != null)
            {
                LocalReturnUrlHelper.TryParseLocalReturnUri(TempData["ReturnUrl"].ToString(), Url, out effectiveReturnUri);
            }

            ViewBag.ReturnUrl = LocalReturnUrlHelper.FormatReturnPathAndQuery(effectiveReturnUri);

            if (companyId.HasValue || effectiveReturnUri == null)
            {
                return;
            }

            TryPopulateCompanyFromReturnUrl(viewModel, effectiveReturnUri);
        }

        private void TryPopulateCompanyFromReturnUrl(RegisterViewModel viewModel, Uri returnUri)
        {
            var positionId = LocalReturnUrlHelper.ExtractPositionId(returnUri);
            if (!positionId.HasValue)
            {
                return;
            }

            var position = _uow.Positions.Get(positionId.Value);
            if (position != null)
            {
                viewModel.CompanyId = position.CompanyId;
            }
        }

        private ActionResult HandleRegisterPost(RegisterViewModel model, bool isSuperAdmin, Uri returnUri)
        {
            ViewBag.IsSuperAdmin = isSuperAdmin;

            if (model == null)
            {
                return ReturnRegisterView(new RegisterViewModel(), isSuperAdmin, returnUri);
            }

            var registrationModel = model;

            if (!ModelState.IsValid)
            {
                return ReturnRegisterView(registrationModel, isSuperAdmin, returnUri);
            }

            if (!model.AcceptLegalTerms)
            {
                ModelState.AddModelError("AcceptLegalTerms", "You must agree to the candidate Terms & Conditions and Privacy Policy to continue.");
                return ReturnRegisterView(model, isSuperAdmin, returnUri);
            }

            var companyResolutionFailure = EnsureRegistrationCompanyContext(model, isSuperAdmin, returnUri);
            if (companyResolutionFailure != null)
            {
                return companyResolutionFailure;
            }

            var validationError = ValidateRegistrationRules(model);
            if (validationError != null)
            {
                return ReturnRegisterValidationError(model, isSuperAdmin, returnUri, validationError);
            }

            try
            {
                var user = CreateRegisteredUser(model);
                CreateRegistrationApplicant(model);
                AuditSvc.LogAction(
                    User.Identity.Name,
                    "REGISTER",
                    "Account",
                    user.Id.ToString(),
                    string.Format("New user registered: {0} {1} ({2}, {3})", user.FirstName, user.LastName, user.UserName, user.Email));

                SignInRegisteredUser(user);
                SetPreferredTenantAfterRegistration(model.CompanyId);

                var safeRegisterReturn = ResolveRegisterSafeReturn(returnUri);
                if (safeRegisterReturn != null)
                {
                    return safeRegisterReturn;
                }

                var tenantToken = RouteData.Values["tenant"] as string;
                return RedirectToAction("Index", "Positions", new { tenant = tenantToken });
            }
            catch (Exception ex)
            {
                AuditSvc.LogAction(User.Identity.Name, "REGISTER_ERROR", "Account", "", "Registration failed: " + ex.Message);
                ModelState.AddModelError("", "Registration failed. Please try again.");
                return ReturnRegisterView(model, isSuperAdmin, returnUri);
            }
        }

        private ActionResult EnsureRegistrationCompanyContext(RegisterViewModel model, bool isSuperAdmin, Uri returnUri)
        {
            if (isSuperAdmin)
            {
                return EnsureSuperAdminCompanySelection(model, returnUri);
            }

            if (model.CompanyId.HasValue)
            {
                return null;
            }

            var companyId = ResolveCompanyIdFromTenantRoute();
            if (companyId.HasValue)
            {
                model.CompanyId = companyId;
                return null;
            }

            ModelState.AddModelError("", "Unable to determine company for registration. Please contact support.");
            return ReturnRegisterView(model, isSuperAdmin, returnUri);
        }

        private ActionResult EnsureSuperAdminCompanySelection(RegisterViewModel model, Uri returnUri)
        {
            if (model.CompanyId.HasValue)
            {
                return null;
            }

            ModelState.AddModelError("CompanyId", "Company selection is required for SuperAdmin registration.");
            return ReturnRegisterView(model, isSuperAdmin: true, returnUri: returnUri);
        }

        private int? ResolveCompanyIdFromTenantRoute()
        {
            var tenantToken = RouteData.Values["tenant"] as string;
            if (string.IsNullOrEmpty(tenantToken))
            {
                return null;
            }

            var company = _uow.Companies.GetAll().FirstOrDefault(c => c.Slug == tenantToken);
            return company != null ? (int?)company.Id : null;
        }

        private RegistrationValidationError ValidateRegistrationRules(RegisterViewModel model)
        {
            var validators = new Func<RegisterViewModel, RegistrationValidationError>[]
            {
                ValidateRegistrationEmailFormat,
                ValidateRegistrationUsernameUniqueness,
                ValidateRegistrationEmailUniqueness,
                ValidateRegistrationPasswordConfirmation,
                ValidateRegistrationPasswordStrength
            };

            foreach (var validator in validators)
            {
                var error = validator(model);
                if (error != null)
                {
                    return error;
                }
            }

            return null;
        }

        private RegistrationValidationError ValidateRegistrationEmailFormat(RegisterViewModel model)
        {
            var email = model.Email ?? string.Empty;
            var domainPart = email.Contains("@") ? email.Split('@').LastOrDefault() : string.Empty;
            if (string.IsNullOrEmpty(domainPart) || !domainPart.Contains("."))
            {
                return new RegistrationValidationError
                {
                    Key = "Email",
                    Message = "Please enter a valid and complete email address."
                };
            }

            return null;
        }

        private RegistrationValidationError ValidateRegistrationUsernameUniqueness(RegisterViewModel model)
        {
            var usernameTakenInCompany = _uow.Context.Users.Any(u => u.UserName == model.UserName && u.CompanyId == model.CompanyId);
            if (!usernameTakenInCompany)
            {
                return null;
            }

            return new RegistrationValidationError
            {
                Key = "UserName",
                Message = "This username is already taken within this company."
            };
        }

        private RegistrationValidationError ValidateRegistrationEmailUniqueness(RegisterViewModel model)
        {
            if (!_uow.Context.Users.Any(u => u.Email == model.Email))
            {
                return null;
            }

            return new RegistrationValidationError
            {
                Key = "Email",
                Message = "This email address is already registered."
            };
        }

        private static RegistrationValidationError ValidateRegistrationPasswordConfirmation(RegisterViewModel model)
        {
            if (model.Password == model.ConfirmPassword)
            {
                return null;
            }

            return new RegistrationValidationError
            {
                Key = "ConfirmPassword",
                Message = "The password and confirmation password do not match."
            };
        }

        private static RegistrationValidationError ValidateRegistrationPasswordStrength(RegisterViewModel model)
        {
            if (PasswordHelper.IsPasswordStrong(model.Password))
            {
                return null;
            }

            return new RegistrationValidationError
            {
                Key = "Password",
                Message = PasswordHelper.GetPasswordStrengthMessage()
            };
        }

        private ActionResult ReturnRegisterValidationError(RegisterViewModel model, bool isSuperAdmin, Uri returnUri, RegistrationValidationError validationError)
        {
            ModelState.AddModelError(validationError.Key, validationError.Message);
            return ReturnRegisterView(model, isSuperAdmin, returnUri);
        }

        private ActionResult ReturnRegisterView(RegisterViewModel model, bool isSuperAdmin, Uri returnUri)
        {
            model.Companies = isSuperAdmin ? GetActiveCompanies() : new List<Company>();
            ViewBag.ReturnUrl = LocalReturnUrlHelper.FormatReturnPathAndQuery(returnUri);

            if (!model.CompanyId.HasValue && returnUri != null)
            {
                TryPopulateCompanyFromReturnUrl(model, returnUri);
            }

            return View("Register", model);
        }

        private User CreateRegisteredUser(RegisterViewModel model)
        {
            var user = new User
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                UserName = model.UserName,
                Email = model.Email,
                Role = "Client",
                PasswordHash = PasswordHelper.HashPassword(model.Password),
                CompanyId = model.CompanyId
            };

            LegalPolicyHelper.ApplyUserAcceptance(user, DateTime.UtcNow, LegalRelationshipKind.Applicant);

            _uow.Users.Add(user);
            _uow.Complete();
            return user;
        }

        private void CreateRegistrationApplicant(RegisterViewModel model)
        {
            var applicant = new Applicant
            {
                FullName = string.Format("{0} {1}", model.FirstName, model.LastName),
                Email = model.Email,
                Phone = model.Phone,
                CompanyId = model.CompanyId
            };

            LegalPolicyHelper.ApplyApplicantAcceptance(applicant, DateTime.UtcNow);

            _uow.Applicants.Add(applicant);
            _uow.Complete();
        }

        private void SignInRegisteredUser(User user)
        {
            user.AccessToken = SecuritySvc.GenerateSecureToken();
            _uow.Users.Update(user);
            _uow.Complete();
            IssueLoginCookie(user, "Client");
        }

        private void SetPreferredTenantAfterRegistration(int? companyId)
        {
            var tenantToken = RouteData.Values["tenant"] as string;
            if (!string.IsNullOrEmpty(tenantToken))
            {
                SetPreferredTenantCookie(tenantToken);
                return;
            }

            if (!companyId.HasValue)
            {
                return;
            }

            var userCompany = _uow.Companies.Get(companyId.Value);
            if (userCompany != null)
            {
                SetPreferredTenantCookie(userCompany.Slug);
            }
        }

        private void SetPreferredTenantCookie(string tenantToken)
        {
            var tenantCookie = new HttpCookie("PreferredTenant", tenantToken)
            {
                Expires = DateTime.Now.AddDays(30),
                Path = "/"
            };

            Response.Cookies.Add(tenantCookie);
        }

        private ActionResult ResolveRegisterSafeReturn(Uri returnUri)
        {
            var tenantToken = RouteData.Values["tenant"] as string;
            return BuildSafeReturnRedirect(returnUri, tenantToken);
        }

        private Uri ParseRegisterReturnPath(string returnPath)
        {
            var raw = !string.IsNullOrWhiteSpace(returnPath)
                ? returnPath
                : Request.Form["ReturnUrl"];
            LocalReturnUrlHelper.TryParseLocalReturnUri(raw, Url, out var parsedUri);
            return parsedUri;
        }
    }
}
