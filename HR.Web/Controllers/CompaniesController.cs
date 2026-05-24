using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Helpers;
using HR.Web.Services;
using HR.Web.Filters;
using Newtonsoft.Json;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [RoleBasedAuthorization("SuperAdmin")]
    public partial class CompaniesController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly TenantService _tenantService;
        private readonly AuditService _auditService;

        public CompaniesController()
        {
            _tenantService = new TenantService(_uow);
            _auditService = new AuditService();
        }

        private string GetCompaniesActorName()
        {
            return User?.Identity?.Name ?? "System";
        }

        /// <summary>
        /// Companies Dashboard
        /// </summary>
        public ActionResult Index()
        {
            var companies = _uow.Companies.GetAll().ToList();
            bool needsSaving = false;

            // Self-healing: Ensure all active companies have an AccessToken
            foreach (var c in companies.Where(x => string.IsNullOrEmpty(x.AccessToken)))
            {
                c.AccessToken = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                _uow.Companies.Update(c);
                needsSaving = true;
            }

            // needsSaving is true if tokens were generated
            if (needsSaving) _uow.Complete();

            var viewModel = new CompaniesDashboardViewModel
            {
                TotalCompanies = companies.Count,
                ActiveCompanies = companies.Count(c => c.IsActive),
                InactiveCompanies = companies.Count(c => !c.IsActive),
                ExpiringSoon = companies.Count(c => c.LicenseExpiryDate.HasValue && 
                    c.LicenseExpiryDate.Value <= DateTime.Now.AddDays(30) &&
                    c.LicenseExpiryDate.Value > DateTime.Now),
                ExpiredCompanies = companies.Count(c => c.LicenseExpiryDate.HasValue && 
                    c.LicenseExpiryDate.Value < DateTime.Now),
                Companies = companies.Select(c => new CompanySummaryViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    AccessToken = c.AccessToken,
                    IsActive = c.IsActive,
                    LicenseExpiryDate = c.LicenseExpiryDate,
                    CreatedDate = c.CreatedDate,
                    UserCount = _uow.Users.GetAll().Count(u => u.CompanyId == c.Id),
                    PositionCount = _uow.Positions.GetAll().Count(p => p.CompanyId == c.Id),
                    ApplicationCount = _uow.Applications.GetAll().Count(a => a.CompanyId == c.Id),
                    DaysUntilExpiry = c.LicenseExpiryDate.HasValue ? 
                        (int)(c.LicenseExpiryDate.Value - DateTime.Now).TotalDays : (int?)null
                }).ToList()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Create new company
        /// </summary>
        public ActionResult CreateCompany()
        {
            return View(new CreateCompanyViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCompany(CreateCompanyViewModel model)
        {
            return HandleCreateCompany(model);
        }

        /// <summary>
        /// One-time secure download of admin credentials
        /// </summary>
        [AllowAnonymous]
        public ActionResult DownloadCredentials(string token)
        {
            return HandleDownloadCredentials(token);
        }

        /// <summary>
        /// Edit company details
        /// </summary>
        public ActionResult EditCompany(int id)
        {
            var company = _uow.Companies.Get(id);
            if (company == null)
                return HttpNotFound();

            var viewModel = new EditCompanyViewModel
            {
                Id = company.Id,
                Name = company.Name,
                Slug = company.Slug,
                IsActive = company.IsActive,
                LicenseExpiryDate = company.LicenseExpiryDate,
                CurrentLogoPath = company.LogoPath,
                CurrentLogoUrl = CompanyLogoHelper.GetPublicUrl(company.LogoPath, Url)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCompany(EditCompanyViewModel model, HttpPostedFileBase logoFile, bool removeLogo = false)
        {
            if (!ModelState.IsValid)
            {
                model.CurrentLogoUrl = CompanyLogoHelper.GetPublicUrl(model.CurrentLogoPath, Url);
                return View(model);
            }

            try
            {
                var company = _uow.Companies.Get(model.Id);
                if (company == null)
                {
                    return HttpNotFound();
                }

                if (logoFile != null && logoFile.ContentLength > 0 && removeLogo)
                {
                    ModelState.AddModelError("", "Choose either a new logo upload or remove the current logo, not both.");
                    model.CurrentLogoPath = company.LogoPath;
                    model.CurrentLogoUrl = CompanyLogoHelper.GetPublicUrl(company.LogoPath, Url);
                    return View(model);
                }

                if (company.Slug != model.Slug)
                {
                    var existing = _uow.Companies.GetAll().FirstOrDefault(c => c.Slug == model.Slug && c.Id != model.Id);
                    if (existing != null)
                    {
                        ModelState.AddModelError("Slug", "This slug is already in use by another company.");
                        model.CurrentLogoPath = company.LogoPath;
                        model.CurrentLogoUrl = CompanyLogoHelper.GetPublicUrl(company.LogoPath, Url);
                        return View(model);
                    }
                }

                var oldValues = new {
                    Name = company.Name,
                    Slug = company.Slug,
                    IsActive = company.IsActive,
                    LicenseExpiry = company.LicenseExpiryDate,
                    LogoPath = company.LogoPath
                };

                company.Name = model.Name;
                company.Slug = model.Slug;
                company.IsActive = model.IsActive;
                company.LicenseExpiryDate = model.LicenseExpiryDate;

                try
                {
                    CompanyLogoHelper.ApplyLogoUpdate(company, logoFile, removeLogo, Server);
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                    model.CurrentLogoPath = company.LogoPath;
                    model.CurrentLogoUrl = CompanyLogoHelper.GetPublicUrl(company.LogoPath, Url);
                    return View(model);
                }

                _uow.Companies.Update(company);
                _uow.Complete();

                _auditService.LogUpdate(
                    GetCompaniesActorName(),
                    "Company",
                    company.Id.ToString(),
                    oldValues,
                    new {
                        Name = company.Name,
                        Slug = company.Slug,
                        IsActive = company.IsActive,
                        LicenseExpiry = company.LicenseExpiryDate,
                        LogoPath = company.LogoPath
                    }
                );

                TempData["SuccessMessage"] = string.Format("Company '{0}' updated successfully.", company.Name);
                return RedirectToAction("CompanyDetails", new { id = company.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error updating company: " + ex.Message);
                model.CurrentLogoUrl = CompanyLogoHelper.GetPublicUrl(model.CurrentLogoPath, Url);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadCompanyLogo(int id, HttpPostedFileBase logoFile)
        {
            var company = _uow.Companies.Get(id);
            if (company == null)
            {
                return HttpNotFound();
            }

            if (logoFile == null || logoFile.ContentLength <= 0)
            {
                TempData["ErrorMessage"] = "Select a logo file to upload.";
                return RedirectToAction("CompanyDetails", new { id });
            }

            try
            {
                var previousPath = company.LogoPath;
                company.LogoPath = CompanyLogoHelper.SaveUploadedLogo(company.Id, logoFile, Server);
                _uow.Companies.Update(company);
                _uow.Complete();
                CompanyLogoHelper.DeleteLogoFile(previousPath, Server);

                _auditService.LogUpdate(
                    GetCompaniesActorName(),
                    "Company",
                    company.Id.ToString(),
                    new { LogoPath = previousPath },
                    new { LogoPath = company.LogoPath });

                TempData["SuccessMessage"] = "Company logo updated.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error uploading logo: " + ex.Message;
            }

            return RedirectToAction("CompanyDetails", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveCompanyLogo(int id)
        {
            var company = _uow.Companies.Get(id);
            if (company == null)
            {
                return HttpNotFound();
            }

            var previousPath = company.LogoPath;
            CompanyLogoHelper.DeleteLogoFile(previousPath, Server);
            company.LogoPath = null;
            _uow.Companies.Update(company);
            _uow.Complete();

            _auditService.LogUpdate(
                GetCompaniesActorName(),
                "Company",
                company.Id.ToString(),
                new { LogoPath = previousPath },
                new { LogoPath = (string)null });

            TempData["SuccessMessage"] = "Company logo removed.";
            return RedirectToAction("CompanyDetails", new { id });
        }

        /// <summary>
        /// View company details
        /// </summary>
        public ActionResult CompanyDetails(int id)
        {
            return HandleCompanyDetails(id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestImpersonation(int companyId, string targetAdmin, string reason)
        {
            if (string.IsNullOrWhiteSpace(targetAdmin))
            {
                TempData["ErrorMessage"] = "A target administrator is required.";
                return RedirectToAction("CompanyDetails", new { id = companyId });
            }

            var request = new ImpersonationRequest
            {
                CompanyId = companyId,
                RequestedBy = GetCompaniesActorName(),
                RequestedFrom = targetAdmin,
                RequestDate = DateTime.Now,
                Status = ImpersonationRequestStatus.Pending,
                Reason = reason,
                ExpiryDate = DateTime.Now.AddHours(24)
            };

            _uow.ImpersonationRequests.Add(request);
            _uow.Complete();

            _auditService.LogAction(
                GetCompaniesActorName(),
                "IMPERSONATION_REQUESTED",
                "Companies",
                companyId.ToString(),
                null,
                new { TargetAdmin = targetAdmin, Reason = reason }
            );

            TempData["SuccessMessage"] = "Impersonation request sent to " + targetAdmin + ". Please wait for approval.";
            return RedirectToAction("CompanyDetails", new { id = companyId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelImpersonationRequest(int requestId)
        {
            var request = _uow.ImpersonationRequests.Get(requestId);
            if (request == null || request.RequestedBy != GetCompaniesActorName()) return HttpNotFound();

            if (request.Status == ImpersonationRequestStatus.Pending)
            {
                request.Status = ImpersonationRequestStatus.Cancelled;
                request.DecisionDate = DateTime.Now;
                _uow.ImpersonationRequests.Update(request);
                _uow.Complete();

                _auditService.LogAction(
                    GetCompaniesActorName(),
                    "IMPERSONATION_CANCELLED",
                    "Companies",
                    request.CompanyId.HasValue ? request.CompanyId.Value.ToString() : null,
                    null,
                    new { RequestId = requestId }
                );

                TempData["SuccessMessage"] = "Impersonation request cancelled.";
            }

            return RedirectToAction("CompanyDetails", new { id = request.CompanyId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Elevate(int requestId)
        {
            return HandleElevate(requestId);
        }

        public ActionResult StopImpersonating()
        {
            int? companyId = null;
            if (_tenantService.IsImpersonating())
            {
                companyId = _tenantService.GetImpersonatedCompanyId();
                _auditService.LogAction(
                    GetCompaniesActorName(),
                    "IMPERSONATION_STOP",
                    "Companies",
                    companyId.ToString(),
                    null,
                    null
                );

                // Ensure the specific request is expired so it can never be reused
                // Clean up any 'Active' or 'Approved' requests for this user/company to prevent auto-login loops
                if (companyId.HasValue)
                {
                    var relatedRequests = _uow.ImpersonationRequests.GetAll()
                        .Where(r => r.CompanyId == companyId.Value && 
                               r.RequestedBy == GetCompaniesActorName() && 
                               (r.Status == ImpersonationRequestStatus.Active || r.Status == ImpersonationRequestStatus.Approved))
                        .ToList();
                    
                    foreach(var r in relatedRequests)
                    {
                        r.Status = ImpersonationRequestStatus.Expired;
                        _uow.ImpersonationRequests.Update(r);
                    }
                }

                _uow.Complete();

                Session.Remove("ImpersonatedRequestId");
                Session.Remove("ImpersonatedCompanyId");
                Session.Remove("ImpersonationReason");
                Session.Remove("ImpersonatedCompanyName");
                Session.Remove("ImpersonationExpiry");
                
                TempData["SuccessMessage"] = "Impersonation session closed. Access rights have been revoked.";
            }

            // Redirect back to company details so they can see they've returned to SuperAdmin role
            if (companyId.HasValue)
            {
                return RedirectToAction("CompanyDetails", new { id = companyId.Value });
            }

            return RedirectToAction("Index");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCompany(int id)
        {
            return HandleDeleteCompany(id);
        }

        /// <summary>
        /// Update existing company slugs to token format
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateSlugs()
        {
            try
            {
                var companies = _uow.Companies.GetAll().ToList();
                int updatedCount = 0;

                foreach (var company in companies)
                {
                    // Generate new token-style slug using cryptographically secure randomness
                    string newSlug = GenerateSecureTokenSlug();

                    // Ensure slug is unique
                    var originalSlug = newSlug;
                    int counter = 1;
                    while (_uow.Companies.GetAll().Any(c => c.Slug.Equals(newSlug, StringComparison.OrdinalIgnoreCase) && c.Id != company.Id))
                    {
                        newSlug = string.Concat(originalSlug, "-", counter);
                        counter++;
                    }

                    // Update company if slug changed
                    if (company.Slug != newSlug)
                    {
                        company.Slug = newSlug;
                        _uow.Companies.Update(company);
                        updatedCount++;
                    }
                }

                if (updatedCount > 0)
                {
                    _uow.Complete();
                    _auditService.LogAction(
                        GetCompaniesActorName(),
                        "COMPANY_SLUGS_UPDATED",
                        "Company",
                        null,
                        null,
                        new { UpdatedCount = updatedCount }
                    );
                    TempData["SuccessMessage"] = string.Format("Updated {0} company slugs to token format.", updatedCount);
                }
                else
                {
                    TempData["InfoMessage"] = "All company slugs are already in token format.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating company slugs: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Generate correct slugs for all existing companies (SuperAdmin only)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateSlugs()
        {
            try
            {
                var companies = _uow.Companies.GetAll().ToList();
                int updatedCount = 0;

                foreach (var company in companies)
                {
                    // Skip if slug already looks correct (contains letters and hyphens, not just GUID)
                    if (!string.IsNullOrEmpty(company.Slug) && 
                        company.Slug.Any(c => char.IsLetter(c)) && 
                        !company.Slug.Equals(company.AccessToken, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip companies that already have proper slugs
                    }

                    // Generate proper slug from company name
                    string newSlug = GenerateSlugFromName(company.Name);
                    
                    // Ensure uniqueness
                    string originalSlug = newSlug;
                    int counter = 1;
                    while (companies.Any(c => c.Id != company.Id && c.Slug.Equals(newSlug, StringComparison.OrdinalIgnoreCase)))
                    {
                        newSlug = originalSlug + "-" + counter;
                        counter++;
                        
                        if (newSlug.Length > 50)
                            newSlug = originalSlug.Substring(0, Math.Max(0, 50 - (counter.ToString().Length + 1))) + "-" + counter;
                    }

                    // Update company slug
                    company.Slug = newSlug;
                    _uow.Companies.Update(company);
                    updatedCount++;
                }

                _uow.Complete();

                _auditService.LogAction(
                    GetCompaniesActorName(),
                    "COMPANY_SLUGS_GENERATED",
                    "Companies",
                    null,
                    null,
                    new { CompaniesUpdated = updatedCount }
                );

                TempData["SuccessMessage"] = string.Format("Generated correct slugs for {0} companies. Refresh page to see updated URLs.", updatedCount);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error generating slugs: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Generate URL-friendly slug from company name (same as TenantService)
        /// </summary>
        private string GenerateSlugFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "company-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // Convert to lowercase and remove invalid characters
            var slug = name.ToLower()
                .Replace(" ", "-")
                .Replace("&", "and")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("[", "")
                .Replace("]", "")
                .Replace("{", "")
                .Replace("}", "")
                .Replace("|", "-");

            // Remove multiple consecutive hyphens
            while (slug.Contains("--"))
                slug = slug.Replace("--", "-");

            // Remove leading/trailing hyphens and limit to 50 chars
            slug = slug.Trim('-').Trim();
            
            if (slug.Length > 50)
                slug = slug.Substring(0, 50);

            return slug;
        }

        private static string GenerateSecureTokenSlug()
        {
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers = "0123456789";

            var slugChars = new char[9];
            slugChars[0] = letters[GetSecureRandomInt(letters.Length)];

            for (int i = 1; i < slugChars.Length; i++)
            {
                slugChars[i] = numbers[GetSecureRandomInt(numbers.Length)];
            }

            return new string(slugChars);
        }

        private static int GetSecureRandomInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException("maxExclusive");
            }

            var bytes = new byte[4];
            var bound = (uint)maxExclusive;
            var max = uint.MaxValue - (uint.MaxValue % bound);
            uint value;

            using (var rng = RandomNumberGenerator.Create())
            {
                do
                {
                    rng.GetBytes(bytes);
                    value = BitConverter.ToUInt32(bytes, 0);
                } while (value >= max);
            }

            return (int)(value % bound);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_uow != null)
                {
                    _uow.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }

    // View Models
    public class CompaniesDashboardViewModel
    {
        public int TotalCompanies { get; set; }
        public int ActiveCompanies { get; set; }
        public int InactiveCompanies { get; set; }
        public int ExpiringSoon { get; set; }
        public int ExpiredCompanies { get; set; }
        public System.Collections.Generic.List<CompanySummaryViewModel> Companies { get; set; }
    }

    public class CompanySummaryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string AccessToken { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LicenseExpiryDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public int UserCount { get; set; }
        public int PositionCount { get; set; }
        public int ApplicationCount { get; set; }
        public int? DaysUntilExpiry { get; set; }
    }

    public class CreateCompanyViewModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Name { get; set; }

        public DateTime? LicenseExpiryDate { get; set; }
        
        public bool IgnoreSimilarNameWarning { get; set; }
    }

    public class EditCompanyViewModel
    {
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Name { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string Slug { get; set; }

        public bool IsActive { get; set; }
        public DateTime? LicenseExpiryDate { get; set; }
        public string CurrentLogoPath { get; set; }
        public Uri CurrentLogoUrl { get; set; }
    }

    public class CompanyDetailsViewModel
    {
        public Company Company { get; set; }
        public System.Collections.Generic.List<User> Users { get; set; }
        public System.Collections.Generic.IDictionary<int, string> UserRoleDisplayNames { get; set; }
        public System.Collections.Generic.List<Position> Positions { get; set; }
        public System.Collections.Generic.List<Application> Applications { get; set; }
        public System.Collections.Generic.List<Department> Departments { get; set; }
        public System.Collections.Generic.List<LicenseTransaction> LicenseTransactions { get; set; }
        public System.Collections.Generic.List<AuditLog> RecentAuditLogs { get; set; }
        public System.Collections.Generic.List<ImpersonationRequest> PendingImpersonationRequests { get; set; }
        public ImpersonationRequest ActiveApprovedRequest { get; set; }
        public ImpersonationRequest ActiveRejectedRequest { get; set; }
        public System.Collections.Generic.List<User> CompanyAdmins { get; set; }
    }
}
