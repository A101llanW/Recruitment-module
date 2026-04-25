using System;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [RoleBasedAuthorization("SuperAdmin")]
    public class LicensesController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly TenantService _tenantService;
        private readonly AuditService _auditService;

        public LicensesController()
        {
            _tenantService = new TenantService(_uow);
            _auditService = new AuditService();
        }

        public ActionResult Index()
        {
            var companies = _uow.Companies.GetAll().OrderBy(c => c.Name).ToList();
            return View(companies);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExtendLicense(int id, int value, string unit)
        {
            try
            {
                var company = _uow.Companies.Get(id);
                if (company == null)
                {
                    return Json(new { success = false, message = "Company not found." });
                }

                var oldExpiry = company.LicenseExpiryDate;
                var newExpiry = CalculateNewExpiry(oldExpiry, value, unit);
                _tenantService.UpdateCompanyLicense(id, newExpiry, null);
                RecordLicenseExtension(id, oldExpiry, newExpiry, value, unit);
                LogLicenseExtension(id, oldExpiry, newExpiry, value, unit);

                return Json(new { 
                    success = true, 
                    message = string.Format("License extended by {0} {1} for {2}.", value, unit, company.Name),
                    newExpiry = newExpiry.ToString("yyyy-MM-dd")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private static DateTime CalculateNewExpiry(DateTime? currentExpiry, int value, string unit)
        {
            var baseDate = currentExpiry ?? DateTime.Now;
            if (string.Equals(unit, "days", StringComparison.OrdinalIgnoreCase))
            {
                return baseDate.AddDays(value);
            }

            return baseDate.AddMonths(value);
        }

        private void RecordLicenseExtension(int companyId, DateTime? oldExpiry, DateTime newExpiry, int value, string unit)
        {
            var transaction = new LicenseTransaction
            {
                CompanyId = companyId,
                ExecutedBy = User.Identity.Name,
                TransactionDate = DateTime.Now,
                PreviousExpiry = oldExpiry,
                NewExpiry = newExpiry,
                ExtendedByValue = value,
                ExtendedByUnit = unit,
                Notes = "License manually extended via Companies panel."
            };

            _uow.LicenseTransactions.Add(transaction);
            _uow.Complete();
        }

        private void LogLicenseExtension(int companyId, DateTime? oldExpiry, DateTime newExpiry, int value, string unit)
        {
            _auditService.LogAction(
                User.Identity.Name,
                "LICENSE_EXTENDED",
                "Company",
                companyId.ToString(),
                new { OldExpiry = oldExpiry },
                new { NewExpiry = newExpiry, ExtendedBy = value, Unit = unit });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStatus(int id)
        {
            try
            {
                var company = _uow.Companies.Get(id);
                if (company == null)
                    return Json(new { success = false, message = "Company not found." });

                company.IsActive = !company.IsActive;
                _uow.Companies.Update(company);
                _uow.Complete();

                _auditService.LogAction(
                    User.Identity.Name,
                    company.IsActive ? "LICENSE_ACTIVATED" : "LICENSE_REVOKED",
                    "Company",
                    id.ToString(),
                    null,
                    new { IsActive = company.IsActive }
                );

                return Json(new { 
                    success = true, 
                    isActive = company.IsActive,
                    message = string.Format("License for {0} {1} successfully.", company.Name, company.IsActive ? "activated" : "revoked")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_uow != null) _uow.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
