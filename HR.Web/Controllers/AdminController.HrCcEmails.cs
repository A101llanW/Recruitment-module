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
            var isSuperAdmin = _tenantService.IsActualSuperAdmin() || User.IsInRole("SuperAdmin");
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var targetCompanyId = isSuperAdmin ? companyId : actorCompanyId;

            var vm = new HrCcEmailsPageViewModel
            {
                IsSuperAdmin = isSuperAdmin,
                CompanyChoices = isSuperAdmin ? _uow.Companies.GetAll().OrderBy(c => c.Name).ToList() : null,
                Contacts = new System.Collections.Generic.List<CompanyHrCcEmail>(),
                NewEntry = new HrCcEmailAddViewModel(),
                CompanyUsersForHr = new System.Collections.Generic.List<UserEmailChoiceForHrCc>()
            };

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

            if (!isSuperAdmin && (!actorCompanyId.HasValue || actorCompanyId.Value != targetCompanyId.Value))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            vm.CompanyId = targetCompanyId.Value;
            vm.CompanyName = company.Name;
            vm.Contacts = _uow.Context.CompanyHrCcEmails
                .Where(e => e.CompanyId == targetCompanyId.Value)
                .OrderBy(e => e.SortOrder)
                .ThenBy(e => e.Email)
                .ToList();
            vm.NewEntry = new HrCcEmailAddViewModel { CompanyId = targetCompanyId.Value };
            var usedEmails = new System.Collections.Generic.HashSet<string>(
                vm.Contacts.Where(c => c.Email != null).Select(c => c.Email.Trim()),
                StringComparer.OrdinalIgnoreCase);
            var companyUsers = _uow.Users.GetAll()
                .Where(u =>
                    u.CompanyId == targetCompanyId.Value &&
                    u.Email != null &&
                    u.Email != string.Empty)
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
            return View(vm);
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
            var isSuperAdmin = _tenantService.IsActualSuperAdmin() || User.IsInRole("SuperAdmin");
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var model = page != null ? page.NewEntry : null;
            if (model == null)
            {
                TempData["ErrorMessage"] = "Invalid form submission.";
                return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? (int?)null : actorCompanyId });
            }

            var targetCompanyId = isSuperAdmin ? model.CompanyId : actorCompanyId.GetValueOrDefault();
            if (targetCompanyId <= 0)
            {
                TempData["ErrorMessage"] = "Company is required.";
                return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? (int?)null : actorCompanyId });
            }

            if (!isSuperAdmin && actorCompanyId != targetCompanyId)
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            model.CompanyId = targetCompanyId;

            if (addFromUserId.HasValue && addFromUserId.Value > 0)
            {
                var pickUser = _uow.Users.GetAll()
                    .FirstOrDefault(u => u.Id == addFromUserId.Value && u.CompanyId == targetCompanyId);
                if (pickUser == null || string.IsNullOrWhiteSpace(pickUser.Email))
                {
                    TempData["ErrorMessage"] = "The selected user was not found or has no email on file.";
                    return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? (int?)targetCompanyId : null });
                }

                model.Email = pickUser.Email.Trim();
                if (string.IsNullOrWhiteSpace(model.Label))
                {
                    model.Label = string.Format("{0} {1}", pickUser.FirstName ?? string.Empty, pickUser.LastName ?? string.Empty).Trim();
                }
            }

            var normalizedEmail = (model.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                TempData["ErrorMessage"] = "Enter an email address or choose a company user from the list.";
                return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? (int?)targetCompanyId : null });
            }

            if (!new EmailAddressAttribute().IsValid(normalizedEmail))
            {
                TempData["ErrorMessage"] = "That email address is not valid.";
                return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? (int?)targetCompanyId : null });
            }

            if (!string.IsNullOrWhiteSpace(model.Label) && model.Label.Length > 150)
            {
                TempData["ErrorMessage"] = "Label must be 150 characters or fewer.";
                return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? (int?)targetCompanyId : null });
            }

            var duplicate = _uow.Context.CompanyHrCcEmails.Any(e =>
                e.CompanyId == targetCompanyId &&
                e.Email.ToLower() == normalizedEmail.ToLower());
            if (duplicate)
            {
                TempData["ErrorMessage"] = "That email address is already in the list for this company.";
                return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? (int?)model.CompanyId : null });
            }

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

            TempData["SuccessMessage"] = "HR CC address added.";
            return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? (int?)model.CompanyId : null });
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

            var isSuperAdmin = _tenantService.IsActualSuperAdmin() || User.IsInRole("SuperAdmin");
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            if (!isSuperAdmin && (!actorCompanyId.HasValue || actorCompanyId.Value != entry.CompanyId))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            if (isSuperAdmin && companyId.HasValue && companyId.Value != entry.CompanyId)
            {
                return new HttpStatusCodeResult(400, "Company mismatch.");
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
            return RedirectToAction("HrCcEmails", new { companyId = isSuperAdmin ? (int?)companySnapshot : null });
        }
    }
}
