using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        /// <summary>
        /// Global user management for SuperAdmins and Admins.
        /// Views: Global users (no company), all Admins, and users per company.
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        public ActionResult GlobalUserManagement()
        {
            return BuildGlobalUserManagementView();
        }

        /// <summary>
        /// Deletes a user permanently including all associated records (applications, interviews, audit logs, etc.)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteUser(int id)
        {
            return HandleDeleteUser(id);
        }

        private ActionResult HandleUserRoleUpdate(UserRoleUpdateViewModel model)
        {
            var isActualSuperAdmin = _tenantService.IsActualSuperAdmin();
            if (!ModelState.IsValid)
            {
                return ReturnUserRoleView(model, isActualSuperAdmin);
            }

            var user = _uow.Users.Get(model.UserId);
            if (user == null)
            {
                return HttpNotFound();
            }

            ValidateRoleUpdateIdentityUniqueness(user, model);
            if (!ModelState.IsValid)
            {
                return ReturnUserRoleView(model, isActualSuperAdmin);
            }

            var tenantAccessResult = ValidateRoleUpdateTenantAccess(user, isActualSuperAdmin);
            if (tenantAccessResult != null)
            {
                return tenantAccessResult;
            }

            var roleRestrictionResult = ValidateRoleChangeRestrictions(user, model, isActualSuperAdmin);
            if (roleRestrictionResult != null)
            {
                return roleRestrictionResult;
            }

            var snapshot = CaptureUserRoleSnapshot(user);
            if (!TryApplyUserRoleUpdates(user, model, snapshot, isActualSuperAdmin))
            {
                return ReturnUserRoleView(model, isActualSuperAdmin);
            }

            var persistenceResult = PersistUserRoleUpdate(user, model, snapshot, isActualSuperAdmin);
            if (persistenceResult != null)
            {
                return persistenceResult;
            }

            var updatedCurrentUser = IsCurrentRequestUser(user, snapshot);
            if (updatedCurrentUser)
            {
                RefreshCurrentUserAuthenticationContext(user);
            }

            SetUserRoleUpdateSuccess(user, snapshot.OldRoleDisplay, updatedCurrentUser);
            return RedirectAfterUserRoleUpdate(user, updatedCurrentUser, isActualSuperAdmin);
        }

        private ActionResult ReturnUserRoleView(UserRoleUpdateViewModel model, bool isActualSuperAdmin)
        {
            if (isActualSuperAdmin)
            {
                model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
            }

            model.AvailableRoleOptions = BuildAvailableRoleOptions(
                isActualSuperAdmin,
                _tenantService.GetCurrentUserCompanyId(),
                model.CompanyId,
                false,
                model.SelectedRoleKey);

            return View("UpdateUserRole", model);
        }

        private void ValidateRoleUpdateIdentityUniqueness(User user, UserRoleUpdateViewModel model)
        {
            if (user.UserName != model.UserName)
            {
                var existingUserByName = _uow.Users.GetAll().FirstOrDefault(u => u.UserName == model.UserName && u.Id != user.Id);
                if (existingUserByName != null)
                {
                    ModelState.AddModelError("UserName", "This username is already taken.");
                }
            }

            if (user.Email != model.Email)
            {
                var existingUserByEmail = _uow.Users.GetAll().FirstOrDefault(u => u.Email == model.Email && u.Id != user.Id);
                if (existingUserByEmail != null)
                {
                    ModelState.AddModelError("Email", "This email address is already in use.");
                }
            }
        }

        private ActionResult ValidateRoleUpdateTenantAccess(User user, bool isActualSuperAdmin)
        {
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && user.CompanyId != companyId.Value && !isActualSuperAdmin)
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return null;
        }

        private ActionResult ValidateRoleChangeRestrictions(User user, UserRoleUpdateViewModel model, bool isActualSuperAdmin)
        {
            if (isActualSuperAdmin)
            {
                return null;
            }

            if (user.Role == "SuperAdmin")
            {
                TempData["ErrorMessage"] = "Admin users cannot modify SuperAdmin accounts.";
                return RedirectToAction("UserManagement");
            }

            if (string.Equals(model.SelectedRoleKey, "builtin:SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Admin users cannot assign SuperAdmin role to any user.";
                return RedirectToAction("UserManagement");
            }

            return null;
        }

        private UserRoleSnapshot CaptureUserRoleSnapshot(User user)
        {
            return new UserRoleSnapshot
            {
                OldRole = user.Role,
                OldRoleDefinitionId = user.RoleDefinitionId,
                OldRoleDisplay = _rolePermissionService.GetDisplayRole(user),
                OldCompanyId = user.CompanyId,
                OldFirstName = user.FirstName,
                OldLastName = user.LastName,
                OldUserName = user.UserName,
                OldEmail = user.Email
            };
        }

        private bool TryApplyUserRoleUpdates(User user, UserRoleUpdateViewModel model, UserRoleSnapshot snapshot, bool isActualSuperAdmin)
        {
            if (user.Email != model.Email)
            {
                var existingUserByEmail = _uow.Users.GetAll().FirstOrDefault(u => u.Email == model.Email && u.Id != user.Id);
                if (existingUserByEmail != null)
                {
                    ModelState.AddModelError("Email", "This email address is already in use.");
                    return false;
                }

                user.AccessToken = _securityService.GenerateSecureToken();
            }

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.UserName = model.UserName;
            user.Email = model.Email;
            user.Phone = model.Phone;

            if (snapshot.OldUserName != user.UserName || snapshot.OldRole != user.Role)
            {
                user.AccessToken = _securityService.GenerateSecureToken();
            }

            if (isActualSuperAdmin)
            {
                user.CompanyId = model.CompanyId;
                user.RequirePasswordChange = model.RequirePasswordChange;
            }

            var roleSelection = ResolveRoleSelection(
                model.SelectedRoleKey,
                isActualSuperAdmin,
                _tenantService.GetCurrentUserCompanyId(),
                user.CompanyId);
            if (!roleSelection.IsValid)
            {
                ModelState.AddModelError("SelectedRoleKey", roleSelection.ErrorMessage);
                return false;
            }

            user.Role = roleSelection.BaseRole;
            user.RoleDefinitionId = roleSelection.RoleDefinitionId;
            model.NewRole = roleSelection.BaseRole;

            if (snapshot.OldUserName != user.UserName ||
                snapshot.OldRole != user.Role ||
                snapshot.OldRoleDefinitionId != user.RoleDefinitionId)
            {
                user.AccessToken = _securityService.GenerateSecureToken();
            }

            return true;
        }

        private ActionResult PersistUserRoleUpdate(User user, UserRoleUpdateViewModel model, UserRoleSnapshot snapshot, bool isActualSuperAdmin)
        {
            try
            {
                SyncApplicantForUserRoleUpdate(user, model, snapshot.OldEmail);
                _uow.Complete();

                _auditService.LogUpdate(
                    User.Identity.Name,
                    "Account",
                    user.Id.ToString(),
                    new
                    {
                        FirstName = snapshot.OldFirstName,
                        LastName = snapshot.OldLastName,
                        UserName = snapshot.OldUserName,
                        Email = snapshot.OldEmail,
                        Role = snapshot.OldRole,
                        RoleDefinitionId = snapshot.OldRoleDefinitionId,
                        CompanyId = snapshot.OldCompanyId
                    },
                    new
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        UserName = user.UserName,
                        Email = user.Email,
                        Role = model.NewRole,
                        RoleDefinitionId = user.RoleDefinitionId,
                        CompanyId = user.CompanyId
                    });

                return null;
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                TempData["ErrorMessage"] = "Data Validation Error: " + BuildValidationErrorMessage(ex);
                return ReturnUserRoleView(model, isActualSuperAdmin);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating user: " + ex.Message;
                return ReturnUserRoleView(model, isActualSuperAdmin);
            }
        }

        private void SyncApplicantForUserRoleUpdate(User user, UserRoleUpdateViewModel model, string oldEmail)
        {
            var applicant = _uow.Context.Set<Applicant>().FirstOrDefault(a => a.Email == oldEmail);
            if (applicant != null)
            {
                applicant.FullName = string.Format("{0} {1}", model.FirstName, model.LastName);
                applicant.Email = model.Email;
                applicant.Phone = model.Phone;
                applicant.CompanyId = user.CompanyId;
                return;
            }

            if (model.NewRole != "Client")
            {
                return;
            }

            var newApplicant = new Applicant
            {
                FullName = string.Format("{0} {1}", model.FirstName, model.LastName),
                Email = user.Email,
                Phone = model.Phone,
                CompanyId = user.CompanyId
            };
            _uow.Applicants.Add(newApplicant);
        }

        private string BuildValidationErrorMessage(System.Data.Entity.Validation.DbEntityValidationException ex)
        {
            var errorMessages = new List<string>();
            foreach (var validationErrors in ex.EntityValidationErrors)
            {
                foreach (var validationError in validationErrors.ValidationErrors)
                {
                    var message = string.Format("Property: {0} Error: {1}", validationError.PropertyName, validationError.ErrorMessage);
                    errorMessages.Add(message);
                    System.Diagnostics.Debug.WriteLine(message);
                }
            }

            return string.Join("; ", errorMessages);
        }

        private void SetUserRoleUpdateSuccess(User user, string oldRole, bool updatedCurrentUser)
        {
            var successMessage = string.Format("User {0} updated successfully.", user.UserName);
            var newDisplayRole = _rolePermissionService.GetDisplayRole(user);
            if (!string.Equals(oldRole, newDisplayRole, StringComparison.OrdinalIgnoreCase))
            {
                successMessage += string.Format(" Role changed from {0} to {1}.", oldRole, newDisplayRole);
            }

            TempData["SuccessMessage"] = successMessage;

            if (updatedCurrentUser)
            {
                Session["IsActualSuperAdmin"] = IsUserEffectiveSuperAdmin(user);
            }
        }

        private ActionResult RedirectAfterUserRoleUpdate(User user, bool updatedCurrentUser, bool isActualSuperAdmin)
        {
            if (updatedCurrentUser)
            {
                if (IsUserEffectiveSuperAdmin(user))
                {
                    return RedirectToAction("GlobalUserManagement");
                }

                if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Index", "Dashboard");
                }

                return RedirectToAction("Index", "Positions");
            }

            if (isActualSuperAdmin || User.IsInRole("SuperAdmin"))
            {
                return RedirectToAction("GlobalUserManagement");
            }

            return RedirectToAction("UserManagement");
        }

        private bool IsCurrentRequestUser(User user, UserRoleSnapshot snapshot)
        {
            var currentIdentityName = User != null && User.Identity != null ? User.Identity.Name : null;
            if (string.IsNullOrWhiteSpace(currentIdentityName))
            {
                return false;
            }

            var currentCompanyId = _tenantService.GetCurrentUserCompanyId();
            var matchesOldIdentity = string.Equals(currentIdentityName, snapshot.OldUserName, StringComparison.OrdinalIgnoreCase) &&
                                     currentCompanyId == snapshot.OldCompanyId;
            var matchesNewIdentity = string.Equals(currentIdentityName, user.UserName, StringComparison.OrdinalIgnoreCase) &&
                                     currentCompanyId == user.CompanyId;

            return matchesOldIdentity || matchesNewIdentity;
        }

        private void RefreshCurrentUserAuthenticationContext(User user)
        {
            if (user == null)
            {
                return;
            }

            var effectiveRole = ResolveAuthTicketRole(user);
            var uaHash = ComputeUserAgentFingerprint(Request != null ? Request.UserAgent : null);
            var userData = string.Format("{0}|{1}|{2}|{3}", effectiveRole, user.CompanyId, user.AccessToken, uaHash);

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
                Secure = Request != null && Request.IsSecureConnection
            };
            Response.Cookies.Add(cookie);

            if (user.CompanyId.HasValue)
            {
                HttpContext.Items["AuthenticatedCompanyId"] = user.CompanyId.Value;
            }
            else if (HttpContext.Items.Contains("AuthenticatedCompanyId"))
            {
                HttpContext.Items.Remove("AuthenticatedCompanyId");
            }

            if (!IsUserEffectiveSuperAdmin(user) && Session != null)
            {
                Session.Remove("ImpersonatedRequestId");
                Session.Remove("ImpersonatedCompanyId");
                Session.Remove("ImpersonationReason");
                Session.Remove("ImpersonatedCompanyName");
                Session.Remove("ImpersonationExpiry");
            }

            var identity = new GenericIdentity(user.UserName, "Forms");
            var principal = new GenericPrincipal(identity, new[] { effectiveRole });
            HttpContext.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;
        }

        private static string ResolveAuthTicketRole(User user)
        {
            if (user == null)
            {
                return "Client";
            }

            var role = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            if (!user.CompanyId.HasValue &&
                (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
            {
                return "SuperAdmin";
            }

            return role;
        }

        private static bool IsUserEffectiveSuperAdmin(User user)
        {
            if (user == null)
            {
                return false;
            }

            return !user.CompanyId.HasValue &&
                   (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(user.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase));
        }

        private static string ComputeUserAgentFingerprint(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return "unknown";
            }

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(userAgent));
                return Convert.ToBase64String(hash).Substring(0, 16);
            }
        }

        private class UserRoleSnapshot
        {
            public string OldRole { get; set; }
            public int? OldRoleDefinitionId { get; set; }
            public string OldRoleDisplay { get; set; }
            public int? OldCompanyId { get; set; }
            public string OldFirstName { get; set; }
            public string OldLastName { get; set; }
            public string OldUserName { get; set; }
            public string OldEmail { get; set; }
        }
    }
}
