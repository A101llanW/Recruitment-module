using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        public ActionResult ApplicationNotifyRecipients(int? companyId = null)
        {
            var isSuperAdmin = IsApplicationNotifySuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var targetCompanyId = isSuperAdmin ? companyId : actorCompanyId;
            var vm = BuildInitialApplicationNotifyViewModel(isSuperAdmin);

            if (!targetCompanyId.HasValue)
            {
                vm.CompanyName = isSuperAdmin ? "Select a company" : "No company context";
                return View(vm);
            }

            var company = _uow.Companies.Get(targetCompanyId.Value);
            if (company == null)
            {
                return HttpNotFound();
            }

            if (!CanAccessApplicationNotifyCompany(isSuperAdmin, actorCompanyId, targetCompanyId.Value))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            PopulateApplicationNotifyCompanyData(vm, company, targetCompanyId.Value);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddApplicationNotifyRecipient(ApplicationNotifyRecipientsPageViewModel page, int? addFromUserId = null)
        {
            var isSuperAdmin = IsApplicationNotifySuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var model = page != null ? page.NewEntry : null;
            if (model == null)
            {
                return RedirectWithApplicationNotifyError(isSuperAdmin, actorCompanyId, null, "Invalid form submission.");
            }

            var targetCompanyId = isSuperAdmin ? model.CompanyId : actorCompanyId.GetValueOrDefault();
            if (targetCompanyId <= 0)
            {
                return RedirectWithApplicationNotifyError(isSuperAdmin, actorCompanyId, null, "Company is required.");
            }

            if (!CanAccessApplicationNotifyCompany(isSuperAdmin, actorCompanyId, targetCompanyId))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            model.CompanyId = targetCompanyId;
            var userPopulateError = TryPopulateNotifyEmailFromUser(model, addFromUserId, targetCompanyId);
            if (userPopulateError != null)
            {
                return RedirectWithApplicationNotifyError(isSuperAdmin, actorCompanyId, targetCompanyId, userPopulateError);
            }

            var validationError = ValidateApplicationNotifyRecipientInput(model, targetCompanyId, excludeId: null);
            if (validationError != null)
            {
                return RedirectWithApplicationNotifyError(isSuperAdmin, actorCompanyId, targetCompanyId, validationError);
            }

            PersistApplicationNotifyRecipient(model, targetCompanyId);
            TempData["SuccessMessage"] = "Notification recipient added.";
            return RedirectToApplicationNotifyRecipients(isSuperAdmin, isSuperAdmin ? (int?)targetCompanyId : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateApplicationNotifyRecipient(ApplicationNotifyRecipientEditViewModel model, int? companyId = null)
        {
            var isSuperAdmin = IsApplicationNotifySuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            if (model == null || model.Id <= 0)
            {
                return RedirectWithApplicationNotifyError(isSuperAdmin, actorCompanyId, companyId, "Invalid form submission.");
            }

            var entry = _uow.Context.CompanyApplicationNotifyRecipients.FirstOrDefault(r => r.Id == model.Id);
            if (entry == null)
            {
                return HttpNotFound();
            }

            if (!CanAccessApplicationNotifyCompany(isSuperAdmin, actorCompanyId, entry.CompanyId))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            var validationError = ValidateApplicationNotifyRecipientInput(
                new ApplicationNotifyRecipientAddViewModel
                {
                    Email = model.Email,
                    Label = model.Label,
                    AccessMode = model.AccessMode,
                    CompanyId = entry.CompanyId
                },
                entry.CompanyId,
                excludeId: entry.Id);

            if (validationError != null)
            {
                return RedirectWithApplicationNotifyError(isSuperAdmin, actorCompanyId, entry.CompanyId, validationError);
            }

            var accessModeChanged = !string.Equals(entry.AccessMode, model.AccessMode, StringComparison.OrdinalIgnoreCase);
            entry.Email = model.Email.Trim();
            entry.Label = string.IsNullOrWhiteSpace(model.Label) ? null : model.Label.Trim();
            entry.AccessMode = NormalizeAccessMode(model.AccessMode);
            entry.IsActive = model.IsActive;

            if (accessModeChanged)
            {
                ApplicationNotificationService.RevokeTokensForRecipient(_uow.Context, entry.Id);
            }

            _uow.Complete();

            _auditService.LogAction(
                User.Identity.Name,
                "APP_NOTIFY_RECIPIENT_UPDATED",
                "ApplicationNotifyRecipients",
                entry.Id.ToString(),
                true,
                string.Format("Updated notification recipient {0} for company {1}", entry.Email, entry.CompanyId));

            TempData["SuccessMessage"] = "Notification recipient updated.";
            return RedirectToApplicationNotifyRecipients(isSuperAdmin, isSuperAdmin ? (int?)entry.CompanyId : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteApplicationNotifyRecipient(int id, int? companyId = null)
        {
            var entry = _uow.Context.CompanyApplicationNotifyRecipients.FirstOrDefault(r => r.Id == id);
            if (entry == null)
            {
                return HttpNotFound();
            }

            var isSuperAdmin = IsApplicationNotifySuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            if (!CanAccessApplicationNotifyCompany(isSuperAdmin, actorCompanyId, entry.CompanyId))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            if (isSuperAdmin && companyId.HasValue && companyId.Value != entry.CompanyId)
            {
                return new HttpStatusCodeResult(400, "Company mismatch.");
            }

            ApplicationNotificationService.RevokeTokensForRecipient(_uow.Context, entry.Id);

            var emailSnapshot = entry.Email;
            var companySnapshot = entry.CompanyId;
            _uow.Context.CompanyApplicationNotifyRecipients.Remove(entry);
            _uow.Complete();

            _auditService.LogAction(
                User.Identity.Name,
                "APP_NOTIFY_RECIPIENT_DELETED",
                "ApplicationNotifyRecipients",
                id.ToString(),
                true,
                string.Format("Removed notification recipient {0} for company {1}", emailSnapshot, companySnapshot));

            TempData["SuccessMessage"] = "Notification recipient removed.";
            return RedirectToApplicationNotifyRecipients(isSuperAdmin, isSuperAdmin ? (int?)companySnapshot : null);
        }

        private ApplicationNotifyRecipientsPageViewModel BuildInitialApplicationNotifyViewModel(bool isSuperAdmin)
        {
            return new ApplicationNotifyRecipientsPageViewModel
            {
                IsSuperAdmin = isSuperAdmin,
                CompanyChoices = isSuperAdmin ? _uow.Companies.GetAll().OrderBy(c => c.Name).ToList() : null,
                Recipients = new System.Collections.Generic.List<CompanyApplicationNotifyRecipient>(),
                NewEntry = new ApplicationNotifyRecipientAddViewModel(),
                CompanyUsersForPick = new System.Collections.Generic.List<UserEmailChoiceForHrCc>()
            };
        }

        private void PopulateApplicationNotifyCompanyData(ApplicationNotifyRecipientsPageViewModel vm, Company company, int targetCompanyId)
        {
            vm.CompanyId = targetCompanyId;
            vm.CompanyName = company.Name;
            vm.Recipients = _uow.Context.CompanyApplicationNotifyRecipients
                .Where(r => r.CompanyId == targetCompanyId)
                .OrderBy(r => r.SortOrder)
                .ThenBy(r => r.Email)
                .ToList();
            vm.NewEntry = new ApplicationNotifyRecipientAddViewModel { CompanyId = targetCompanyId };

            var usedEmails = new System.Collections.Generic.HashSet<string>(
                vm.Recipients.Where(r => r.Email != null).Select(r => r.Email.Trim()),
                StringComparer.OrdinalIgnoreCase);
            var companyUsers = _uow.Users.GetAll()
                .Where(u => u.CompanyId == targetCompanyId && u.Email != null && u.Email != string.Empty)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToList();
            vm.CompanyUsersForPick = companyUsers
                .Where(u => !usedEmails.Contains(u.Email.Trim()))
                .Select(u => new UserEmailChoiceForHrCc
                {
                    Id = u.Id,
                    Email = SanitizeChoiceText(u.Email),
                    DisplayName = string.Format("{0} {1}", SanitizeChoiceText(u.FirstName), SanitizeChoiceText(u.LastName)).Trim()
                })
                .ToList();
        }

        private void PersistApplicationNotifyRecipient(ApplicationNotifyRecipientAddViewModel model, int targetCompanyId)
        {
            var nextOrder = _uow.Context.CompanyApplicationNotifyRecipients
                .Where(r => r.CompanyId == targetCompanyId)
                .Select(r => (int?)r.SortOrder)
                .Max() ?? 0;

            var entry = new CompanyApplicationNotifyRecipient
            {
                CompanyId = targetCompanyId,
                Email = model.Email.Trim(),
                Label = string.IsNullOrWhiteSpace(model.Label) ? null : model.Label.Trim(),
                AccessMode = NormalizeAccessMode(model.AccessMode),
                SortOrder = nextOrder + 1,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _uow.Context.CompanyApplicationNotifyRecipients.Add(entry);
            _uow.Complete();

            _auditService.LogAction(
                User.Identity.Name,
                "APP_NOTIFY_RECIPIENT_ADDED",
                "ApplicationNotifyRecipients",
                entry.Id.ToString(),
                true,
                string.Format("Added notification recipient {0} for company {1}", entry.Email, targetCompanyId));
        }

        private string TryPopulateNotifyEmailFromUser(ApplicationNotifyRecipientAddViewModel model, int? addFromUserId, int targetCompanyId)
        {
            if (!addFromUserId.HasValue || addFromUserId.Value <= 0)
            {
                return null;
            }

            var pickUser = _uow.Users.GetAll()
                .FirstOrDefault(u => u.Id == addFromUserId.Value && u.CompanyId == targetCompanyId);
            if (pickUser == null || string.IsNullOrWhiteSpace(pickUser.Email))
            {
                return "The selected user was not found or has no email on file.";
            }

            model.Email = pickUser.Email.Trim();
            if (string.IsNullOrWhiteSpace(model.Label))
            {
                model.Label = string.Format("{0} {1}", pickUser.FirstName ?? string.Empty, pickUser.LastName ?? string.Empty).Trim();
            }

            return null;
        }

        private string ValidateApplicationNotifyRecipientInput(ApplicationNotifyRecipientAddViewModel model, int targetCompanyId, int? excludeId)
        {
            var normalizedEmail = (model.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return "Enter an email address or choose a company user from the list.";
            }

            if (!new EmailAddressAttribute().IsValid(normalizedEmail))
            {
                return "That email address is not valid.";
            }

            if (!string.IsNullOrWhiteSpace(model.Label) && model.Label.Length > 150)
            {
                return "Label must be 150 characters or fewer.";
            }

            if (!IsValidAccessMode(model.AccessMode))
            {
                return "Select a valid access mode.";
            }

            var duplicate = _uow.Context.CompanyApplicationNotifyRecipients.Any(r =>
                r.CompanyId == targetCompanyId &&
                r.Email.ToLower() == normalizedEmail.ToLower() &&
                (!excludeId.HasValue || r.Id != excludeId.Value));
            if (duplicate)
            {
                return "That email address is already in the notification list for this company.";
            }

            model.Email = normalizedEmail;
            model.AccessMode = NormalizeAccessMode(model.AccessMode);
            return null;
        }

        private static bool IsValidAccessMode(string accessMode)
        {
            return string.Equals(accessMode, ApplicationNotifyAccessModes.LoginRequired, StringComparison.OrdinalIgnoreCase)
                || string.Equals(accessMode, ApplicationNotifyAccessModes.PublicReadOnly, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAccessMode(string accessMode)
        {
            return string.Equals(accessMode, ApplicationNotifyAccessModes.PublicReadOnly, StringComparison.OrdinalIgnoreCase)
                ? ApplicationNotifyAccessModes.PublicReadOnly
                : ApplicationNotifyAccessModes.LoginRequired;
        }

        private bool IsApplicationNotifySuperAdmin()
        {
            return _tenantService.IsActualSuperAdmin() || User.IsInRole("SuperAdmin");
        }

        private static bool CanAccessApplicationNotifyCompany(bool isSuperAdmin, int? actorCompanyId, int targetCompanyId)
        {
            return isSuperAdmin || (actorCompanyId.HasValue && actorCompanyId.Value == targetCompanyId);
        }

        private ActionResult RedirectToApplicationNotifyRecipients(bool isSuperAdmin, int? companyId)
        {
            return RedirectToAction("ApplicationNotifyRecipients", new { companyId = isSuperAdmin ? companyId : null });
        }

        private ActionResult RedirectWithApplicationNotifyError(bool isSuperAdmin, int? actorCompanyId, int? targetCompanyId, string message)
        {
            TempData["ErrorMessage"] = message;
            var companyId = targetCompanyId ?? (isSuperAdmin ? (int?)null : actorCompanyId);
            return RedirectToApplicationNotifyRecipients(isSuperAdmin, companyId);
        }
    }
}
