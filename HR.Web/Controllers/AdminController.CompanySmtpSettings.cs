using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        private readonly CompanySmtpSettingsService _companySmtpSettingsService = new CompanySmtpSettingsService();

        public ActionResult CompanySmtpSettings(int? companyId = null)
        {
            var isSuperAdmin = IsCompanySmtpSuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var targetCompanyId = isSuperAdmin ? companyId : actorCompanyId;
            var vm = BuildInitialCompanySmtpSettingsViewModel(isSuperAdmin);

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

            if (!CanAccessCompanySmtpSettings(isSuperAdmin, actorCompanyId, targetCompanyId.Value))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            PopulateCompanySmtpSettingsData(vm, company, targetCompanyId.Value);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCompanySmtpSettings(CompanySmtpSettingsPageViewModel page)
        {
            var isSuperAdmin = IsCompanySmtpSuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var form = page != null ? page.Form : null;
            if (form == null)
            {
                return RedirectWithCompanySmtpError(isSuperAdmin, actorCompanyId, null, "Invalid form submission.");
            }

            var targetCompanyId = isSuperAdmin ? form.CompanyId : actorCompanyId.GetValueOrDefault();
            if (targetCompanyId <= 0)
            {
                return RedirectWithCompanySmtpError(isSuperAdmin, actorCompanyId, null, "Company is required.");
            }

            if (!CanAccessCompanySmtpSettings(isSuperAdmin, actorCompanyId, targetCompanyId))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            try
            {
                _companySmtpSettingsService.Save(targetCompanyId, MapFormToInput(form));
                _auditService.LogAction(
                    User.Identity.Name,
                    "COMPANY_SMTP_SETTINGS_SAVED",
                    "CompanySmtpSettings",
                    targetCompanyId.ToString(),
                    true,
                    string.Format("Updated company SMTP settings (enabled={0}) for company {1}", form.IsEnabled, targetCompanyId));

                TempData["SuccessMessage"] = form.IsEnabled
                    ? "Company SMTP settings saved. Candidate emails for this company will use these settings."
                    : "Company SMTP settings saved. This company will use the global email configuration.";
            }
            catch (ArgumentException ex)
            {
                return RedirectWithCompanySmtpError(isSuperAdmin, actorCompanyId, targetCompanyId, ex.Message);
            }

            return RedirectToCompanySmtpSettings(isSuperAdmin, isSuperAdmin ? (int?)targetCompanyId : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendCompanySmtpTestEmail(CompanySmtpTestEmailViewModel model, int? companyId = null)
        {
            var isSuperAdmin = IsCompanySmtpSuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var targetCompanyId = isSuperAdmin ? (model != null ? model.CompanyId : companyId.GetValueOrDefault()) : actorCompanyId.GetValueOrDefault();

            if (targetCompanyId <= 0)
            {
                return RedirectWithCompanySmtpError(isSuperAdmin, actorCompanyId, null, "Company is required.");
            }

            if (!CanAccessCompanySmtpSettings(isSuperAdmin, actorCompanyId, targetCompanyId))
            {
                return new HttpStatusCodeResult(403, "Access denied.");
            }

            if (model == null || string.IsNullOrWhiteSpace(model.TestRecipient))
            {
                return RedirectWithCompanySmtpError(isSuperAdmin, actorCompanyId, targetCompanyId, "Enter a recipient email address for the test send.");
            }

            var smtpConfig = _companySmtpSettingsService.ResolveForCompany(targetCompanyId);
            if (!smtpConfig.IsCompanyScoped)
            {
                return RedirectWithCompanySmtpError(
                    isSuperAdmin,
                    actorCompanyId,
                    targetCompanyId,
                    "Company SMTP is not enabled or complete. Save valid settings before sending a test email.");
            }

            var emailService = new EmailService(new SettingsService(), _companySmtpSettingsService);
            var subject = "Test email - " + AppConfig.ProductName;
            var body = string.Format(
                @"<p>This is a test message from your company SMTP settings in {0}.</p>
<p>If you received this email, outbound mail for your company is configured correctly.</p>",
                AppConfig.ProductName);

            try
            {
                await emailService.SendAsync(model.TestRecipient.Trim(), subject, body, targetCompanyId);
                TempData["SuccessMessage"] = string.Format("Test email sent to {0}.", model.TestRecipient.Trim());
            }
            catch (Exception)
            {
                return RedirectWithCompanySmtpError(
                    isSuperAdmin,
                    actorCompanyId,
                    targetCompanyId,
                    "Test email could not be sent. Check your SMTP settings and try again.");
            }

            return RedirectToCompanySmtpSettings(isSuperAdmin, isSuperAdmin ? (int?)targetCompanyId : null);
        }

        private CompanySmtpSettingsPageViewModel BuildInitialCompanySmtpSettingsViewModel(bool isSuperAdmin)
        {
            return new CompanySmtpSettingsPageViewModel
            {
                IsSuperAdmin = isSuperAdmin,
                CompanyChoices = isSuperAdmin ? _uow.Companies.GetAll().OrderBy(c => c.Name).ToList() : null,
                Form = new CompanySmtpSettingsFormModel()
            };
        }

        private void PopulateCompanySmtpSettingsData(CompanySmtpSettingsPageViewModel vm, Company company, int targetCompanyId)
        {
            vm.CompanyId = targetCompanyId;
            vm.CompanyName = company.Name;
            vm.HasStoredPassword = _companySmtpSettingsService.HasConfiguredPassword(targetCompanyId);

            var settings = _companySmtpSettingsService.GetForCompany(targetCompanyId);
            var resolved = _companySmtpSettingsService.ResolveForCompany(targetCompanyId);
            vm.UsesGlobalFallback = !resolved.IsCompanyScoped;

            vm.Form = new CompanySmtpSettingsFormModel
            {
                CompanyId = targetCompanyId,
                IsEnabled = settings != null && settings.IsEnabled,
                SmtpHost = settings != null ? settings.SmtpHost : string.Empty,
                SmtpPort = settings != null && settings.SmtpPort > 0 ? settings.SmtpPort : 587,
                SmtpUser = settings != null ? settings.SmtpUser : string.Empty,
                SmtpEnableSsl = settings == null || settings.SmtpEnableSsl,
                FromEmail = settings != null ? settings.FromEmail : string.Empty,
                FromName = settings != null ? settings.FromName : company.Name
            };
        }

        private static CompanySmtpSettingsInput MapFormToInput(CompanySmtpSettingsFormModel form)
        {
            return new CompanySmtpSettingsInput
            {
                IsEnabled = form.IsEnabled,
                SmtpHost = form.SmtpHost,
                SmtpPort = form.SmtpPort,
                SmtpUser = form.SmtpUser,
                SmtpPassword = form.SmtpPassword,
                SmtpEnableSsl = form.SmtpEnableSsl,
                FromEmail = form.FromEmail,
                FromName = form.FromName,
                ClearStoredPassword = form.ClearStoredPassword
            };
        }

        private bool IsCompanySmtpSuperAdmin()
        {
            return _tenantService.IsActualSuperAdmin() || User.IsInRole("SuperAdmin");
        }

        private static bool CanAccessCompanySmtpSettings(bool isSuperAdmin, int? actorCompanyId, int targetCompanyId)
        {
            return isSuperAdmin || (actorCompanyId.HasValue && actorCompanyId.Value == targetCompanyId);
        }

        private ActionResult RedirectToCompanySmtpSettings(bool isSuperAdmin, int? companyId)
        {
            return RedirectToAction("CompanySmtpSettings", new { companyId = isSuperAdmin ? companyId : null });
        }

        private ActionResult RedirectWithCompanySmtpError(bool isSuperAdmin, int? actorCompanyId, int? targetCompanyId, string message)
        {
            TempData["ErrorMessage"] = message;
            var companyId = targetCompanyId ?? (isSuperAdmin ? (int?)null : actorCompanyId);
            return RedirectToCompanySmtpSettings(isSuperAdmin, companyId);
        }
    }
}
