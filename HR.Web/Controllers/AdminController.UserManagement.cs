using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
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
        /// Allows Admins and SuperAdmins to elevate any user to SuperAdmin role.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult ElevateToSuperAdmin(int id)
        {
            var user = _uow.Users.Get(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var oldRole = user.Role;
            user.Role = "SuperAdmin";
            _uow.Users.Update(user);
            _uow.Complete();

            // Log the elevation
            _auditService.LogAction(
                User.Identity.Name, 
                "ELEVATE_TO_SUPERADMIN", 
                "UserManagement", 
                user.Id.ToString(), 
                true, 
                string.Format("Elevated user {0} from {1} to SuperAdmin", user.UserName, oldRole)
            );

            TempData["Message"] = string.Format("User {0} has been elevated to SuperAdmin.", user.UserName);
            return RedirectToAction("GlobalUserManagement");
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

            SetUserRoleUpdateSuccess(user, snapshot.OldRole, isActualSuperAdmin);
            return RedirectAfterUserRoleUpdate(isActualSuperAdmin);
        }

        private ActionResult ReturnUserRoleView(UserRoleUpdateViewModel model, bool isActualSuperAdmin)
        {
            if (isActualSuperAdmin)
            {
                model.Companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
            }

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

            if (model.NewRole == "SuperAdmin")
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
            user.Role = model.NewRole;

            if (snapshot.OldUserName != user.UserName || snapshot.OldRole != user.Role)
            {
                user.AccessToken = _securityService.GenerateSecureToken();
            }

            if (isActualSuperAdmin)
            {
                user.CompanyId = model.CompanyId;
                user.RequirePasswordChange = model.RequirePasswordChange;
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
                        CompanyId = snapshot.OldCompanyId
                    },
                    new
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        UserName = user.UserName,
                        Email = user.Email,
                        Role = model.NewRole,
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

        private void SetUserRoleUpdateSuccess(User user, string oldRole, bool isActualSuperAdmin)
        {
            var successMessage = string.Format("User {0} updated successfully.", user.UserName);
            if (oldRole != user.Role)
            {
                successMessage += string.Format(" Role changed from {0} to {1}.", oldRole, user.Role);
            }

            TempData["SuccessMessage"] = successMessage;

            if (user.UserName == User.Identity.Name)
            {
                Session["IsActualSuperAdmin"] = isActualSuperAdmin;
            }
        }

        private ActionResult RedirectAfterUserRoleUpdate(bool isActualSuperAdmin)
        {
            if (isActualSuperAdmin || User.IsInRole("SuperAdmin"))
            {
                return RedirectToAction("GlobalUserManagement");
            }

            return RedirectToAction("UserManagement");
        }

        private class UserRoleSnapshot
        {
            public string OldRole { get; set; }
            public int? OldCompanyId { get; set; }
            public string OldFirstName { get; set; }
            public string OldLastName { get; set; }
            public string OldUserName { get; set; }
            public string OldEmail { get; set; }
        }
    }
}
