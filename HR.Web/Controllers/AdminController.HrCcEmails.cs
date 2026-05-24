using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        public ActionResult HrCcEmails(int? companyId = null)
        {
            var isSuperAdmin = IsHrCcSuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var targetCompanyId = isSuperAdmin ? companyId : actorCompanyId;
            var vm = BuildInitialHrCcEmailsViewModel(isSuperAdmin);

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

            if (!CanAccessHrCcCompany(isSuperAdmin, actorCompanyId, targetCompanyId.Value))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            PopulateHrCcEmailsCompanyData(vm, company, targetCompanyId.Value);
            return View(vm);
        }

        private HrCcEmailsPageViewModel BuildInitialHrCcEmailsViewModel(bool isSuperAdmin)
        {
            return new HrCcEmailsPageViewModel
            {
                IsSuperAdmin = isSuperAdmin,
                CompanyChoices = isSuperAdmin ? _uow.Companies.GetAll().OrderBy(c => c.Name).ToList() : null,
                Contacts = new System.Collections.Generic.List<CompanyHrCcEmail>(),
                NewEntry = new HrCcEmailAddViewModel(),
                CompanyUsersForHr = new System.Collections.Generic.List<UserEmailChoiceForHrCc>()
            };
        }

        private void PopulateHrCcEmailsCompanyData(HrCcEmailsPageViewModel vm, Company company, int targetCompanyId)
        {
            vm.CompanyId = targetCompanyId;
            vm.CompanyName = company.Name;
            vm.Contacts = _uow.Context.CompanyHrCcEmails
                .Where(e => e.CompanyId == targetCompanyId)
                .OrderBy(e => e.SortOrder)
                .ThenBy(e => e.Email)
                .ToList();
            vm.NewEntry = new HrCcEmailAddViewModel { CompanyId = targetCompanyId };

            var usedEmails = new System.Collections.Generic.HashSet<string>(
                vm.Contacts.Where(c => c.Email != null).Select(c => c.Email.Trim()),
                StringComparer.OrdinalIgnoreCase);
            var companyUsers = _uow.Users.GetAll()
                .Where(u => u.CompanyId == targetCompanyId && u.Email != null && u.Email != string.Empty)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToList();
            vm.CompanyUsersForHr = companyUsers
                .Where(u => !usedEmails.Contains(u.Email.Trim()))
                .Select(u => new UserEmailChoiceForHrCc
                {
                    Id = u.Id,
                    Email = SanitizeChoiceText(u.Email),
                    DisplayName = string.Format(
                        "{0} {1}",
                        SanitizeChoiceText(u.FirstName),
                        SanitizeChoiceText(u.LastName)).Trim()
                })
                .ToList();
        }

        private bool IsHrCcSuperAdmin()
        {
            return _tenantService.IsActualSuperAdmin() || User.IsInRole("SuperAdmin");
        }

        private static bool CanAccessHrCcCompany(bool isSuperAdmin, int? actorCompanyId, int targetCompanyId)
        {
            return isSuperAdmin || (actorCompanyId.HasValue && actorCompanyId.Value == targetCompanyId);
        }

        private ActionResult RedirectToHrCcEmails(bool isSuperAdmin, int? companyId)
        {
            return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? companyId : null });
        }

        /// <summary>
        /// Normalizes text shown in HTML &lt;option&gt; labels: decode accidental HTML entities and strip control characters.
        /// </summary>
        private static string SanitizeChoiceText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decoded = HttpUtility.HtmlDecode(value.Trim());
            var filtered = new char[decoded.Length];
            var j = 0;
            for (var i = 0; i < decoded.Length; i++)
            {
                var c = decoded[i];
                if (char.IsControl(c) && c != '\t')
                {
                    continue;
                }

                filtered[j++] = c == '\t' ? ' ' : c;
            }

            return new string(filtered, 0, j).Trim();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddHrCcEmail(HrCcEmailsPageViewModel page, int? addFromUserId = null)
        {
            var isSuperAdmin = IsHrCcSuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var model = page != null ? page.NewEntry : null;
            if (model == null)
            {
                return RedirectWithHrCcError(isSuperAdmin, actorCompanyId, null, "Invalid form submission.");
            }

            return ProcessAddHrCcEmail(model, addFromUserId, isSuperAdmin, actorCompanyId);
        }

        private ActionResult ProcessAddHrCcEmail(HrCcEmailAddViewModel model, int? addFromUserId, bool isSuperAdmin, int? actorCompanyId)
        {
            var targetCompanyId = ResolveHrCcAddTargetCompany(isSuperAdmin, actorCompanyId, model);
            if (targetCompanyId <= 0)
            {
                return RedirectWithHrCcError(isSuperAdmin, actorCompanyId, null, "Company is required.");
            }

            if (!CanAccessHrCcCompany(isSuperAdmin, actorCompanyId, targetCompanyId))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            model.CompanyId = targetCompanyId;
            var userPopulateError = TryPopulateHrCcEmailFromUser(model, addFromUserId, targetCompanyId);
            if (userPopulateError != null)
            {
                return RedirectWithHrCcError(isSuperAdmin, actorCompanyId, targetCompanyId, userPopulateError);
            }

            var validationError = ValidateHrCcEmailInput(model, targetCompanyId);
            if (validationError != null)
            {
                return RedirectWithHrCcError(isSuperAdmin, actorCompanyId, targetCompanyId, validationError);
            }

            PersistHrCcEmailEntry(model, targetCompanyId);
            TempData["SuccessMessage"] = "HR CC address added.";
            return RedirectToHrCcEmails(isSuperAdmin, isSuperAdmin ? (int?)model.CompanyId : null);
        }

        private ActionResult RedirectWithHrCcError(bool isSuperAdmin, int? actorCompanyId, int? targetCompanyId, string message)
        {
            TempData["ErrorMessage"] = message;
            var companyId = targetCompanyId ?? (isSuperAdmin ? (int?)null : actorCompanyId);
            return RedirectToHrCcEmails(isSuperAdmin, companyId);
        }

        private void PersistHrCcEmailEntry(HrCcEmailAddViewModel model, int targetCompanyId)
        {
            var normalizedEmail = model.Email.Trim();
            var nextOrder = _uow.Context.CompanyHrCcEmails.Where(e => e.CompanyId == targetCompanyId).Select(e => (int?)e.SortOrder).Max() ?? 0;
            var entry = new CompanyHrCcEmail
            {
                CompanyId = targetCompanyId,
                Email = normalizedEmail,
                Label = string.IsNullOrWhiteSpace(model.Label) ? null : model.Label.Trim(),
                SortOrder = nextOrder + 1,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _uow.Context.CompanyHrCcEmails.Add(entry);
            _uow.Complete();

            _auditService.LogAction(
                User.Identity.Name,
                "HR_CC_EMAIL_ADDED",
                "HrCcEmails",
                entry.Id.ToString(),
                true,
                string.Format("Added HR CC email {0} for company {1}", normalizedEmail, targetCompanyId));
        }

        private static int ResolveHrCcAddTargetCompany(bool isSuperAdmin, int? actorCompanyId, HrCcEmailAddViewModel model)
        {
            return isSuperAdmin ? model.CompanyId : actorCompanyId.GetValueOrDefault();
        }

        private string TryPopulateHrCcEmailFromUser(HrCcEmailAddViewModel model, int? addFromUserId, int targetCompanyId)
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

        private string ValidateHrCcEmailInput(HrCcEmailAddViewModel model, int targetCompanyId)
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

            var duplicate = _uow.Context.CompanyHrCcEmails.Any(e =>
                e.CompanyId == targetCompanyId &&
                e.Email.ToLower() == normalizedEmail.ToLower());
            if (duplicate)
            {
                return "That email address is already in the list for this company.";
            }

            model.Email = normalizedEmail;
            return null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteHrCcEmail(int id, int? companyId = null)
        {
            var entry = _uow.Context.CompanyHrCcEmails.FirstOrDefault(e => e.Id == id);
            if (entry == null)
            {
                return HttpNotFound();
            }

            var accessDenied = ValidateHrCcDeleteAccess(entry, companyId);
            if (accessDenied != null)
            {
                return accessDenied;
            }

            var emailSnapshot = entry.Email;
            var companySnapshot = entry.CompanyId;
            _uow.Context.CompanyHrCcEmails.Remove(entry);
            _uow.Complete();

            _auditService.LogAction(
                User.Identity.Name,
                "HR_CC_EMAIL_DELETED",
                "HrCcEmails",
                id.ToString(),
                true,
                string.Format("Removed HR CC email {0} for company {1}", emailSnapshot, companySnapshot));

            TempData["SuccessMessage"] = "HR CC address removed.";
            var isSuperAdmin = IsHrCcSuperAdmin();
            return RedirectToHrCcEmails(isSuperAdmin, isSuperAdmin ? (int?)companySnapshot : null);
        }

        private ActionResult ValidateHrCcDeleteAccess(CompanyHrCcEmail entry, int? companyId)
        {
            var isSuperAdmin = IsHrCcSuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            if (!CanAccessHrCcCompany(isSuperAdmin, actorCompanyId, entry.CompanyId))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            if (isSuperAdmin && companyId.HasValue && companyId.Value != entry.CompanyId)
            {
                return new HttpStatusCodeResult(400, "Company mismatch.");
            }

            return null;
        }
    }
}
