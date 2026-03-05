using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Helpers;
using HR.Web.Services;
using HR.Web.Filters;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [RoleBasedAuthorization("SuperAdmin")]
    public class CompaniesController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly TenantService _tenantService;
        private readonly AuditService _auditService;

        public CompaniesController()
        {
            _tenantService = new TenantService(_uow);
            _auditService = new AuditService();
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
            if (!ModelState.IsValid)
                return View(model);

            // Prevent duplicate submissions
            var formKey = string.Format("CreateCompany_{0}_{1}", User.Identity.Name, model.Name);
            if (TempData[formKey] != null)
            {
                TempData["ErrorMessage"] = "Duplicate submission detected. Company creation is already in progress.";
                return RedirectToAction("Index");
            }

            TempData[formKey] = true;

            try
            {
                // Check for similar company name
                if (!model.IgnoreSimilarNameWarning)
                {
                    var nameLower = model.Name.ToLower().Trim();
                    var similarCompany = _uow.Companies.GetAll().FirstOrDefault(c => c.Name.ToLower().Contains(nameLower) || nameLower.Contains(c.Name.ToLower()));
                    
                    if (similarCompany != null)
                    {
                        ModelState.AddModelError("Name", string.Format("Warning: A company with a similar name ('{0}') already exists. If you are sure you want to create this company, check the confirmation box below and submit again.", similarCompany.Name));
                        // Unset the submission lock so they can try again
                        TempData.Remove(formKey);
                        return View(model);
                    }
                }
                var company = _tenantService.CreateCompany(
                    model.Name, 
                    model.LicenseExpiryDate ?? DateTime.Now.AddYears(1)
                );

                _auditService.LogAction(
                    User.Identity.Name,
                    "COMPANY_CREATED",
                    "Company",
                    company.Id.ToString(),
                    null,
                    new { CompanyName = company.Name, Slug = company.Slug }
                );

                // Get the created admin user credentials
                var adminUser = _uow.Users.GetAll()
                    .FirstOrDefault(u => u.CompanyId == company.Id && u.Role == "Admin");

                string tempPassword = "";
                if (adminUser != null)
                {
                    // Generate a new temporary password for admin user
                    tempPassword = _tenantService.GenerateDefaultPassword();
                    adminUser.PasswordHash = PasswordHelper.HashPassword(tempPassword);
                    _uow.Users.Update(adminUser);
                    _uow.Complete();
                }

                // Clear the form key
                TempData.Remove(formKey);

                // Store admin credentials in TempData for popup display
                if (adminUser != null && !string.IsNullOrEmpty(tempPassword))
                {
                    TempData["AdminCredentials"] = new AdminCredentialsViewModel
                    {
                        CompanyName = company.Name,
                        CompanyUrl = string.Format("{0}/{1}", 
                            Helpers.ExternalUrlHelper.GetBaseUrl(Request), 
                            company.Slug),
                        AdminUsername = adminUser.UserName,
                        AdminPassword = tempPassword
                    };
                }

                TempData["SuccessMessage"] = string.Format(
                    "Company '{0}' created successfully with URL: <strong>{1}/{2}</strong>", 
                    company.Name, 
                    Helpers.ExternalUrlHelper.GetBaseUrl(Request),
                    company.Slug);
                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                TempData.Remove(formKey);
                var messages = dbEx.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.PropertyName + ": " + x.ErrorMessage);
                ModelState.AddModelError("", "Validation failed: " + string.Join("; ", messages));
                return View(model);
            }
            catch (Exception ex)
            {
                // Clear the form key on error
                TempData.Remove(formKey);
                ModelState.AddModelError("", "Error creating company: " + ex.Message);
                return View(model);
            }
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
                LicenseExpiryDate = company.LicenseExpiryDate
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCompany(EditCompanyViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var company = _uow.Companies.Get(model.Id);
                if (company == null)
                    return HttpNotFound();

                // Check for slug collision if it changed
                if (company.Slug != model.Slug)
                {
                    var existing = _uow.Companies.GetAll().FirstOrDefault(c => c.Slug == model.Slug && c.Id != model.Id);
                    if (existing != null)
                    {
                        ModelState.AddModelError("Slug", "This slug is already in use by another company.");
                        return View(model);
                    }
                }

                var oldValues = new { 
                    Name = company.Name, 
                    Slug = company.Slug,
                    IsActive = company.IsActive, 
                    LicenseExpiry = company.LicenseExpiryDate 
                };

                company.Name = model.Name;
                company.Slug = model.Slug;
                company.IsActive = model.IsActive;
                company.LicenseExpiryDate = model.LicenseExpiryDate;

                _uow.Companies.Update(company);
                _uow.Complete();

                _auditService.LogUpdate(
                    User.Identity.Name,
                    "Company",
                    company.Id.ToString(),
                    oldValues,
                    new { Name = company.Name, Slug = company.Slug, IsActive = company.IsActive, LicenseExpiry = company.LicenseExpiryDate }
                );

                TempData["SuccessMessage"] = string.Format("Company '{0}' updated successfully.", company.Name);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error updating company: " + ex.Message);
                return View(model);
            }
        }

        /// <summary>
        /// View company details
        /// </summary>
        public ActionResult CompanyDetails(int id)
        {
            var company = _uow.Companies.Get(id);
            if (company == null)
                return HttpNotFound();

            // Check for and handle rejection notification
            var rejectionNotification = Session["RejectionNotification"] as dynamic;
            if (rejectionNotification != null && rejectionNotification.CompanyId == id)
            {
                ViewBag.RejectionNotification = rejectionNotification;
                Session["RejectionNotification"] = null; // Clear after displaying
            }

            var viewModel = new CompanyDetailsViewModel
            {
                Company = company,
                Users = _uow.Users.GetAll().Where(u => u.CompanyId == id).ToList(),
                Positions = _uow.Positions.GetAll().Where(p => p.CompanyId == id).ToList(),
                Applications = _uow.Applications.GetAll().Where(a => a.CompanyId == id).ToList(),
                Departments = _uow.Departments.GetAll().Where(d => d.CompanyId == id).ToList(),
                LicenseTransactions = _uow.LicenseTransactions.GetAll()
                    .Where(lt => lt.CompanyId == id)
                    .OrderByDescending(lt => lt.TransactionDate)
                    .ToList(),
                RecentAuditLogs = _uow.AuditLogs.GetAll()
                    .Where(a => a.CompanyId == id)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(20)
                    .ToList(),
                PendingImpersonationRequests = _uow.ImpersonationRequests.GetAll()
                    .Where(r => r.CompanyId == id && r.Status == ImpersonationRequestStatus.Pending && r.RequestedBy == User.Identity.Name)
                    .OrderByDescending(r => r.RequestDate)
                    .ToList(),
                ActiveApprovedRequest = _uow.ImpersonationRequests.GetAll()
                    .FirstOrDefault(r => r.CompanyId == id && 
                                   r.RequestedBy == User.Identity.Name && 
                                   r.Status == ImpersonationRequestStatus.Approved &&
                                   (!r.ExpiryDate.HasValue || r.ExpiryDate > DateTime.Now)),
                ActiveRejectedRequest = null, // Don't show persistent rejection messages
                CompanyAdmins = _uow.Users.GetAll()
                    .Where(u => u.CompanyId == id && u.Role == "Admin")
                    .ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestImpersonation(int companyId, string targetAdmin, string reason = null)
        {
            if (string.IsNullOrWhiteSpace(targetAdmin))
            {
                TempData["ErrorMessage"] = "A target administrator is required.";
                return RedirectToAction("CompanyDetails", new { id = companyId });
            }

            var request = new ImpersonationRequest
            {
                CompanyId = companyId,
                RequestedBy = User.Identity.Name,
                RequestedFrom = targetAdmin,
                RequestDate = DateTime.Now,
                Status = ImpersonationRequestStatus.Pending,
                Reason = reason,
                ExpiryDate = DateTime.Now.AddHours(24)
            };

            _uow.ImpersonationRequests.Add(request);
            _uow.Complete();

            _auditService.LogAction(
                User.Identity.Name,
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
            if (request == null || request.RequestedBy != User.Identity.Name) return HttpNotFound();

            if (request.Status == ImpersonationRequestStatus.Pending)
            {
                request.Status = ImpersonationRequestStatus.Cancelled;
                request.DecisionDate = DateTime.Now;
                _uow.ImpersonationRequests.Update(request);
                _uow.Complete();

                _auditService.LogAction(
                    User.Identity.Name,
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
            var request = _uow.ImpersonationRequests.Get(requestId);
            if (request == null || request.RequestedBy != User.Identity.Name) return HttpNotFound();

            if (request.Status != ImpersonationRequestStatus.Approved)
            {
                TempData["ErrorMessage"] = "This request has not been approved or has expired.";
                return RedirectToAction("CompanyDetails", new { id = request.CompanyId });
            }

            var company = _uow.Companies.Get(request.CompanyId.Value);
            if (company == null) return HttpNotFound();

            // Check expiry
            if (request.ExpiryDate.HasValue && request.ExpiryDate.Value < DateTime.Now)
            {
                request.Status = ImpersonationRequestStatus.Expired;
                _uow.ImpersonationRequests.Update(request);
                _uow.Complete();
                TempData["ErrorMessage"] = "This authorization has expired.";
                return RedirectToAction("CompanyDetails", new { id = request.CompanyId });
            }

            // Log the impersonation event
            _auditService.LogAction(
                User.Identity.Name,
                "IMPERSONATION_START",
                "Companies",
                request.CompanyId.ToString(),
                null,
                new { Reason = request.Reason, CompanyName = company.Name, ApprovedBy = request.RequestedFrom }
            );

            // Set session variables
            Session["ImpersonatedRequestId"] = request.Id;
            Session["ImpersonatedCompanyId"] = request.CompanyId;
            Session["ImpersonationReason"] = request.Reason ?? "Not specified";
            Session["ImpersonatedCompanyName"] = company.Name;
            Session["ImpersonationExpiry"] = request.ExpiryDate;

            // Mark request as Active
            request.Status = ImpersonationRequestStatus.Active;
            _uow.ImpersonationRequests.Update(request);
            _uow.Complete();

            TempData["SuccessMessage"] = string.Format("Now impersonating {0}. Session authorized by {1}. Expiry: {2}", 
                company.Name, request.RequestedFrom, request.ExpiryDate.HasValue ? request.ExpiryDate.Value.ToString("HH:mm") : "N/A");
            return RedirectToAction("Index", "Dashboard");
        }

        public ActionResult StopImpersonating()
        {
            int? companyId = null;
            if (_tenantService.IsImpersonating())
            {
                companyId = _tenantService.GetImpersonatedCompanyId();
                _auditService.LogAction(
                    User.Identity.Name,
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
                               r.RequestedBy == User.Identity.Name && 
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
            try
            {
                var company = _uow.Companies.Get(id);
                if (company == null) return HttpNotFound();

                // Delete all users belonging to this company first (e.g. the default admin)
                var users = _uow.Users.GetAll().Where(u => u.CompanyId == id).ToList();
                foreach(var user in users)
                {
                    _uow.Users.Remove(user);
                }

                // Verify other dependencies
                int positionCount = _uow.Positions.GetAll().Count(p => p.CompanyId == id);
                int appCount = _uow.Applications.GetAll().Count(a => a.CompanyId == id);

                if (positionCount > 0 || appCount > 0)
                {
                    TempData["ErrorMessage"] = "Cannot delete company because it has existing positions or applications.";
                    return RedirectToAction("Index");
                }

                var companyName = company.Name;
                _uow.Companies.Remove(company);
                _uow.Complete();

                _auditService.LogAction(
                    User.Identity.Name,
                    "COMPANY_DELETED",
                    "Company",
                    id.ToString(),
                    new { Name = companyName },
                    null
                );

                TempData["SuccessMessage"] = string.Format("Company '{0}' has been permanently deleted.", companyName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting company: " + ex.Message;
            }

            return RedirectToAction("Index");
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
                    // Generate new token-style slug
                    var random = new Random();
                    string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    string numbers = "0123456789";
                    
                    string newSlug = letters[random.Next(0, 26)].ToString();
                    for (int i = 0; i < 8; i++)
                    {
                        newSlug += numbers[random.Next(0, 10)].ToString();
                    }

                    // Ensure slug is unique
                    var originalSlug = newSlug;
                    int counter = 1;
                    while (_uow.Companies.GetAll().Any(c => c.Slug.Equals(newSlug, StringComparison.OrdinalIgnoreCase) && c.Id != company.Id))
                    {
                        newSlug = originalSlug + "-" + counter;
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
                        User.Identity.Name,
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
                    User.Identity.Name,
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
    }

    public class CompanyDetailsViewModel
    {
        public Company Company { get; set; }
        public System.Collections.Generic.List<User> Users { get; set; }
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
