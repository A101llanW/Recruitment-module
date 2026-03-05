using System;
using System.Linq;
using System.Web;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Helpers;

namespace HR.Web.Services
{
    public class TenantService
    {
        private readonly UnitOfWork _uow;

        public TenantService()
        {
            _uow = new UnitOfWork();
        }

        public TenantService(UnitOfWork uow)
        {
            _uow = uow;
        }

        /// <summary>
        /// Get current user's CompanyId from their authentication context
        /// </summary>
        public int? GetCurrentUserCompanyId()
        {
            if (HttpContext.Current == null) return null;

            // 1. If impersonating, return the impersonated company ID
            if (IsImpersonating())
            {
                var session = HttpContext.Current.Session;
                return session != null ? (int?)session["ImpersonatedCompanyId"] : null;
            }

            // 2. If authenticated, use the CompanyId we stored in Global.asax (from auth ticket)
            if (HttpContext.Current.User != null && HttpContext.Current.User.Identity.IsAuthenticated)
            {
                if (HttpContext.Current.Items.Contains("AuthenticatedCompanyId"))
                {
                    return (int?)HttpContext.Current.Items["AuthenticatedCompanyId"];
                }
                
                // If it's not in Items yet (e.g. during the auth process itself), 
                // we might need to look it up, but only as a fallback.
                // However, for SuperAdmins, null is the correct return value.
            }

            // 3. Fallback to URL-based context
            if (HttpContext.Current.Items.Contains("TenantContext"))
            {
                return (int?)HttpContext.Current.Items["TenantContext"];
            }

            return null;
        }

        /// <summary>
        /// Check if the current user is a SuperAdmin
        /// </summary>
        public bool IsSuperAdmin()
        {
            // If impersonating, the user is technically acting as a tenant, so we return false
            // for general role checks to ensure they are filtered correctly.
            if (IsImpersonating())
            {
                return false;
            }

            return IsActualSuperAdmin();
        }

        public bool IsActualSuperAdmin()
        {
            if (HttpContext.Current == null || HttpContext.Current.User == null || !HttpContext.Current.User.Identity.IsAuthenticated)
                return false;

            // Use the role check directly against the principal
            if (HttpContext.Current.User.IsInRole("SuperAdmin"))
                return true;

            // Special case: "Admin" role with no CompanyId is also a SuperAdmin
            // Check context items directly to avoid recursion with GetCurrentUserCompanyId()
            bool hasCompany = HttpContext.Current.Items.Contains("AuthenticatedCompanyId") && HttpContext.Current.Items["AuthenticatedCompanyId"] != null;
            
            return !hasCompany && HttpContext.Current.User.IsInRole("Admin");
        }

        /// <summary>
        /// Get the current user's role
        /// </summary>
        public string GetCurrentUserRole()
        {
            if (IsImpersonating())
            {
                return "Admin"; // Impersonate as Admin
            }

            if (HttpContext.Current == null || HttpContext.Current.User == null || !HttpContext.Current.User.Identity.IsAuthenticated)
                return null;

            if (HttpContext.Current.User.IsInRole("SuperAdmin")) return "SuperAdmin";
            if (HttpContext.Current.User.IsInRole("Admin")) return "Admin";
            if (HttpContext.Current.User.IsInRole("Client")) return "Client";
            
            return "Client";
        }

        public bool IsImpersonating()
        {
            return HttpContext.Current != null && HttpContext.Current.Session != null && HttpContext.Current.Session["ImpersonatedCompanyId"] != null;
        }

        public int? GetImpersonatedCompanyId()
        {
            return (HttpContext.Current != null && HttpContext.Current.Session != null) ? (int?)HttpContext.Current.Session["ImpersonatedCompanyId"] : null;
        }

        public string GetImpersonationReason()
        {
            return (HttpContext.Current != null && HttpContext.Current.Session != null) ? (string)HttpContext.Current.Session["ImpersonationReason"] : null;
        }

        public IQueryable<T> ApplyTenantFilter<T>(IQueryable<T> query) where T : class, ITenantEntity
        {
            if (IsSuperAdmin())
                return query; // SuperAdmin sees all

            var companyId = GetCurrentUserCompanyId();
            if (!companyId.HasValue)
                return query.Where(e => false); // No company = no data

            return query.Where(e => e.CompanyId == companyId.Value);
        }

        /// <summary>
        /// Public-aware tenant filtering: 
        /// Anonymous users see data from the URL-based tenant.
        /// Authenticated users (non-SuperAdmin) see their own company.
        /// SuperAdmins see everything.
        /// </summary>
        public IQueryable<T> ApplyPublicTenantFilter<T>(IQueryable<T> query) where T : class, ITenantEntity
        {
            if (IsSuperAdmin())
                return query;

            if (HttpContext.Current == null || HttpContext.Current.User == null || !HttpContext.Current.User.Identity.IsAuthenticated)
            {
                // Anonymous access - use URL-based tenant context
                if (HttpContext.Current != null && HttpContext.Current.Items.Contains("TenantContext"))
                {
                    var tenantId = (int?)HttpContext.Current.Items["TenantContext"];
                    if (tenantId.HasValue)
                    {
                        return query.Where(e => e.CompanyId == tenantId.Value);
                    }
                }
                // Anonymous access with no tenant context - show only from active companies
                return query.Where(e => e.Company != null && e.Company.IsActive);
            }

            // For authenticated users, prioritize their actual company over URL context
            var companyId = GetCurrentUserCompanyId();
            if (companyId.HasValue)
            {
                // Authenticated user belongs to a specific company
                return query.Where(e => e.CompanyId == companyId.Value);
            }

            // Fallback to URL-based tenant context only if user has no company
            if (HttpContext.Current != null && HttpContext.Current.Items.Contains("TenantContext"))
            {
                var tenantId = (int?)HttpContext.Current.Items["TenantContext"];
                if (tenantId.HasValue)
                {
                    return query.Where(e => e.CompanyId == tenantId.Value);
                }
            }

            return query.Where(e => false); // No company = no data
        }

        /// <summary>
        /// Check if a company's license is active
        /// </summary>
        public bool IsCompanyLicenseActive(int companyId)
        {
            var company = _uow.Companies.Get(companyId);
            if (company == null)
                return true; // Missing != Expired
            
            if (!company.IsActive)
                return false;

            if (company.LicenseExpiryDate.HasValue && company.LicenseExpiryDate.Value < DateTime.Now)
                return false;

            return true;
        }

        /// <summary>
        /// Check if the current user's company license is active
        /// </summary>
        public bool IsCurrentCompanyLicenseActive()
        {
            var companyId = GetCurrentUserCompanyId();
            if (!companyId.HasValue)
                return IsSuperAdmin(); // SuperAdmin always active

            return IsCompanyLicenseActive(companyId.Value);
        }

        /// <summary>
        /// Get company by ID (SuperAdmin only)
        /// </summary>
        public Company GetCompany(int id)
        {
            if (!IsSuperAdmin())
                throw new UnauthorizedAccessException("Only SuperAdmin can access company details.");

            return _uow.Companies.Get(id);
        }

        /// <summary>
        /// Get all companies (SuperAdmin only)
        /// </summary>
        public IQueryable<Company> GetAllCompanies()
        {
            if (!IsSuperAdmin())
                throw new UnauthorizedAccessException("Only SuperAdmin can list companies.");

            return _uow.Context.Companies.AsQueryable();
        }

        /// <summary>
        /// Create a new company (SuperAdmin only)
        /// </summary>
        public Company CreateCompany(string name, DateTime? licenseExpiry, string customSlug = null)
        {
            if (!IsSuperAdmin())
                throw new UnauthorizedAccessException("Only SuperAdmin can create companies.");

            // Auto-generate slug from company name if not provided
            string slug = customSlug;
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = GenerateSlugFromName(name);
            }

            var company = new Company
            {
                Name = name,
                Slug = slug,
                AccessToken = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(), // Secure URL token
                IsActive = true,
                LicenseExpiryDate = licenseExpiry ?? DateTime.Now.AddYears(1),
                CreatedDate = DateTime.Now
            };

            _uow.Companies.Add(company);
            _uow.Complete();

            // Create admin user for the new company
            CreateCompanyAdmin(company);

            return company;
        }

        /// <summary>
        /// Create admin user for a new company
        /// </summary>
        private void CreateCompanyAdmin(Company company)
        {
            // Generate default admin credentials
            string adminUsername = GenerateAdminUsername(company.Name);
            string defaultPassword = GenerateDefaultPassword();

            var adminUser = new User
            {
                UserName = adminUsername,
                FirstName = "Admin",
                LastName = company.Name.Length > 100 ? company.Name.Substring(0, 100) : company.Name,
                Email = string.Format("PENDING_{0}@hrsystem.local", Guid.NewGuid().ToString("N").Substring(0, 8)), // Setup required on first login
                Role = "Admin",
                CompanyId = company.Id,
                PasswordHash = PasswordHelper.HashPassword(defaultPassword),
                RequirePasswordChange = false
            };

            _uow.Users.Add(adminUser);
            _uow.Complete();
        }

        /// <summary>
        /// Generate admin username from company name
        /// </summary>
        private string GenerateAdminUsername(string companyName)
        {
            var baseName = companyName.ToLower()
                .Replace(" ", "")
                .Replace("&", "and")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("!", "")
                .Replace("?", "")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("/", "")
                .Replace("\\", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("[", "")
                .Replace("]", "")
                .Replace("{", "")
                .Replace("}", "")
                .Replace("|", "");

            // Limit to reasonable length and add admin suffix
            if (baseName.Length > 15)
                baseName = baseName.Substring(0, 15);

            string username = baseName + "admin";
            
            // Ensure username is unique
            int counter = 1;
            while (_uow.Users.GetAll().Any(u => u.UserName.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                username = baseName + "admin" + counter;
                counter++;
            }

            return username;
        }

        /// <summary>
        /// Generate secure default password
        /// </summary>
        public string GenerateDefaultPassword()
        {
            return PasswordHelper.GenerateSecureRandomPassword(12);
        }

        /// <summary>
        /// Generate URL-friendly slug from company name
        /// </summary>
        private string GenerateSlugFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "company-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // Generate access token style slug (e.g., A0809273)
            var random = new Random();
            string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string numbers = "0123456789";
            
            string slug = letters[random.Next(0, 26)].ToString();
            for (int i = 0; i < 8; i++)
            {
                slug += numbers[random.Next(0, 10)].ToString();
            }

            // Ensure slug is unique
            var originalSlug = slug;
            int counter = 1;
            while (_uow.Companies.GetAll().Any(c => c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)))
            {
                slug = originalSlug + "-" + counter;
                counter++;
                
                if (slug.Length > 50)
                    slug = originalSlug.Substring(0, Math.Max(0, 50 - (counter.ToString().Length + 1))) + "-" + counter;
            }

            return slug;
        }

        /// <summary>
        /// Update company license (SuperAdmin only)
        /// </summary>
        public void UpdateCompanyLicense(int companyId, DateTime? newExpiryDate, bool? isActive)
        {
            if (!IsSuperAdmin())
                throw new UnauthorizedAccessException("Only SuperAdmin can update licenses.");

            var company = _uow.Companies.Get(companyId);
            if (company == null)
                throw new ArgumentException("Company not found.");

            if (newExpiryDate.HasValue)
                company.LicenseExpiryDate = newExpiryDate.Value;

            if (isActive.HasValue)
                company.IsActive = isActive.Value;

            _uow.Companies.Update(company);
            _uow.Complete();
        }

        /// <summary>
        /// Deactivate a company (SuperAdmin only)
        /// </summary>
        public void DeactivateCompany(int companyId)
        {
            UpdateCompanyLicense(companyId, null, false);
        }

        /// <summary>
        /// Activate a company (SuperAdmin only)
        /// </summary>
        public void ActivateCompany(int companyId)
        {
            UpdateCompanyLicense(companyId, null, true);
        }
    }
}
