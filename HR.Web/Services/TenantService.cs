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
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }

            _uow = uow;
        }

        /// <summary>
        /// Get current user's CompanyId from their authentication context
        /// </summary>
        public int? GetCurrentUserCompanyId()
        {
            if (HttpContext.Current == null)
            {
                return null;
            }

            int? impersonatedCompanyId;
            if (TryGetImpersonatedCompanyId(out impersonatedCompanyId))
            {
                return impersonatedCompanyId;
            }

            int? authenticatedCompanyId;
            if (TryGetAuthenticatedUserCompanyId(out authenticatedCompanyId))
            {
                return authenticatedCompanyId;
            }

            int? tenantContextId;
            return TryGetContextItemInt("TenantContext", out tenantContextId) ? tenantContextId : null;
        }

        private bool TryGetImpersonatedCompanyId(out int? companyId)
        {
            companyId = null;
            if (!IsImpersonating())
            {
                return false;
            }

            var session = HttpContext.Current.Session;
            companyId = session != null ? (int?)session["ImpersonatedCompanyId"] : null;
            return true;
        }

        private static bool TryGetAuthenticatedUserCompanyId(out int? companyId)
        {
            companyId = null;
            if (HttpContext.Current == null ||
                HttpContext.Current.User == null ||
                !HttpContext.Current.User.Identity.IsAuthenticated)
            {
                return false;
            }

            return TryGetContextItemInt("AuthenticatedCompanyId", out companyId);
        }

        private static bool TryGetContextItemInt(string key, out int? value)
        {
            value = null;
            if (HttpContext.Current == null || !HttpContext.Current.Items.Contains(key))
            {
                return false;
            }

            value = (int?)HttpContext.Current.Items[key];
            return true;
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
            {
                return false;
            }

            if (HttpContext.Current.User.IsInRole("SuperAdmin"))
            {
                return true;
            }

            bool hasCompany = HttpContext.Current.Items.Contains("AuthenticatedCompanyId") &&
                              HttpContext.Current.Items["AuthenticatedCompanyId"] != null;

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
            {
                return null;
            }

            if (HttpContext.Current.User.IsInRole("SuperAdmin"))
            {
                return "SuperAdmin";
            }

            if (HttpContext.Current.User.IsInRole("Admin"))
            {
                return "Admin";
            }

            if (HttpContext.Current.User.IsInRole("Client"))
            {
                return "Client";
            }

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
            {
                return query;
            }

            var companyId = GetCurrentUserCompanyId();
            return FilterByCompanyId(query, companyId);
        }

        private static IQueryable<T> FilterByCompanyId<T>(IQueryable<T> query, int? companyId) where T : class, ITenantEntity
        {
            if (!companyId.HasValue)
            {
                return query.Where(e => false);
            }

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
            {
                return query;
            }

            if (HttpContext.Current == null || HttpContext.Current.User == null || !HttpContext.Current.User.Identity.IsAuthenticated)
            {
                return ApplyAnonymousPublicTenantFilter(query);
            }

            var companyId = GetCurrentUserCompanyId();
            if (companyId.HasValue)
            {
                return FilterByCompanyId(query, companyId);
            }

            int? tenantContextId;
            if (TryGetContextItemInt("TenantContext", out tenantContextId))
            {
                return FilterByCompanyId(query, tenantContextId);
            }

            return query.Where(e => false);
        }

        private static IQueryable<T> ApplyAnonymousPublicTenantFilter<T>(IQueryable<T> query) where T : class, ITenantEntity
        {
            int? tenantId;
            if (TryGetContextItemInt("TenantContext", out tenantId))
            {
                return FilterByCompanyId(query, tenantId);
            }

            return query.Where(e => e.Company != null && e.Company.IsActive);
        }

        /// <summary>
        /// Check if a company's license is active
        /// </summary>
        public bool IsCompanyLicenseActive(int companyId)
        {
            var company = _uow.Companies.Get(companyId);
            if (company == null)
            {
                return true;
            }

            if (!company.IsActive)
            {
                return false;
            }

            if (company.LicenseExpiryDate.HasValue && company.LicenseExpiryDate.Value < DateTime.Now)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if the current user's company license is active
        /// </summary>
        public bool IsCurrentCompanyLicenseActive()
        {
            var companyId = GetCurrentUserCompanyId();
            if (!companyId.HasValue)
            {
                return IsSuperAdmin();
            }

            return IsCompanyLicenseActive(companyId.Value);
        }

        /// <summary>
        /// Get company by ID (SuperAdmin only)
        /// </summary>
        public Company GetCompany(int id)
        {
            if (!IsSuperAdmin())
            {
                throw new UnauthorizedAccessException("Only SuperAdmin can access company details.");
            }

            return _uow.Companies.Get(id);
        }

        /// <summary>
        /// Get all companies (SuperAdmin only)
        /// </summary>
        public IQueryable<Company> GetAllCompanies()
        {
            if (!IsSuperAdmin())
            {
                throw new UnauthorizedAccessException("Only SuperAdmin can list companies.");
            }

            return _uow.Context.Companies.AsQueryable();
        }

        /// <summary>
        /// Create a new company (SuperAdmin only)
        /// </summary>
        public Company CreateCompany(string name, DateTime? licenseExpiry, string customSlug = null)
        {
            if (!IsSuperAdmin())
            {
                throw new UnauthorizedAccessException("Only SuperAdmin can create companies.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Company name is required.");
            }

            var companyName = name;
            string slug = string.IsNullOrWhiteSpace(customSlug) ? GenerateSlugFromName(companyName) : customSlug;

            var company = new Company
            {
                Name = companyName,
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
            if (company == null)
            {
                return;
            }

            var scopedCompany = company;
            string adminUsername = GenerateAdminUsername(scopedCompany.Name);
            string defaultPassword = GenerateDefaultPassword();

            var adminUser = new User
            {
                UserName = adminUsername,
                FirstName = "Admin",
                LastName = scopedCompany.Name.Length > 100 ? scopedCompany.Name.Substring(0, 100) : scopedCompany.Name,
                Email = string.Format("PENDING_{0}@hrsystem.local", Guid.NewGuid().ToString("N").Substring(0, 8)), // Setup required on first login
                Role = "Admin",
                CompanyId = scopedCompany.Id,
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
            var baseName = SanitizeCompanyNameForUsername(companyName ?? "company");

            if (baseName.Length > 15)
            {
                baseName = baseName.Substring(0, 15);
            }

            return EnsureUniqueUsername(baseName + "admin");
        }

        private static string SanitizeCompanyNameForUsername(string companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName))
            {
                return "company";
            }

            return companyName.ToLower()
                .Replace(" ", string.Empty)
                .Replace("&", "and")
                .Replace(".", string.Empty)
                .Replace(",", string.Empty)
                .Replace("!", string.Empty)
                .Replace("?", string.Empty)
                .Replace("'", string.Empty)
                .Replace("\"", string.Empty)
                .Replace("/", string.Empty)
                .Replace("\\", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("[", string.Empty)
                .Replace("]", string.Empty)
                .Replace("{", string.Empty)
                .Replace("}", string.Empty)
                .Replace("|", string.Empty);
        }

        private string EnsureUniqueUsername(string baseUsername)
        {
            string username = baseUsername;
            int counter = 1;
            while (_uow.Users.GetAll().Any(u => u.UserName.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                username = baseUsername + counter;
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
            {
                return "company-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            var slug = GenerateRandomAccessTokenSlug();
            return EnsureUniqueSlug(slug);
        }

        private static string GenerateRandomAccessTokenSlug()
        {
            var bytes = new byte[9];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers = "0123456789";

            var slug = letters[bytes[0] % letters.Length].ToString();
            for (int i = 1; i < 9; i++)
            {
                slug += numbers[bytes[i] % numbers.Length].ToString();
            }

            return slug;
        }

        private string EnsureUniqueSlug(string slug)
        {
            var originalSlug = slug;
            int counter = 1;
            while (_uow.Companies.GetAll().Any(c => c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)))
            {
                slug = originalSlug + "-" + counter;
                counter++;

                if (slug.Length > 50)
                {
                    slug = originalSlug.Substring(0, Math.Max(0, 50 - (counter.ToString().Length + 1))) + "-" + counter;
                }
            }

            return slug;
        }

        /// <summary>
        /// Update company license (SuperAdmin only)
        /// </summary>
        public void UpdateCompanyLicense(int companyId, DateTime? newExpiryDate, bool? isActive)
        {
            if (!IsSuperAdmin())
            {
                throw new UnauthorizedAccessException("Only SuperAdmin can update licenses.");
            }

            var company = _uow.Companies.Get(companyId);
            if (company == null)
            {
                throw new ArgumentException("Company not found.");
            }

            if (newExpiryDate.HasValue)
            {
                company.LicenseExpiryDate = newExpiryDate.Value;
            }

            if (isActive.HasValue)
            {
                company.IsActive = isActive.Value;
            }

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
