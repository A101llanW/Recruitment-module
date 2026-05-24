using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HR.Web.Models;
using HR.Web.Services;
using MvcUrlHelper = System.Web.Mvc.UrlHelper;

namespace HR.Web.Helpers
{
    public static class CompanyLogoHelper
    {
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp"
        };

        public static string GetPublicUrl(string logoPath, MvcUrlHelper urlHelper)
        {
            var logoUri = NavMenuBuilder.ResolveCompanyLogoUri(logoPath, urlHelper);
            return logoUri != null ? logoUri.ToString() : null;
        }

        public static string SaveUploadedLogo(int companyId, HttpPostedFileBase file, HttpServerUtilityBase server)
        {
            if (file == null || file.ContentLength <= 0)
            {
                return null;
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Logo must be PNG, JPG, GIF, or WEBP.");
            }

            var directoryVirtualPath = "~/Content/company-logos";
            var directoryPhysicalPath = server.MapPath(directoryVirtualPath);
            Directory.CreateDirectory(directoryPhysicalPath);

            var fileName = string.Format(
                "company-{0}-{1}{2}",
                companyId,
                Guid.NewGuid().ToString("N"),
                extension.ToLowerInvariant());
            var physicalPath = Path.Combine(directoryPhysicalPath, fileName);
            file.SaveAs(physicalPath);

            return "Content/company-logos/" + fileName;
        }

        public static void DeleteLogoFile(string logoPath, HttpServerUtilityBase server)
        {
            if (string.IsNullOrWhiteSpace(logoPath) || server == null)
            {
                return;
            }

            var relative = logoPath.Trim().Replace('\\', '/').TrimStart('/');
            if (relative.StartsWith("~/", StringComparison.Ordinal))
            {
                relative = relative.Substring(2);
            }

            var physicalPath = server.MapPath("~/" + relative);
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }

        public static void ApplyLogoUpdate(Company company, HttpPostedFileBase logoFile, bool removeLogo, HttpServerUtilityBase server)
        {
            if (company == null)
            {
                return;
            }

            if (removeLogo)
            {
                DeleteLogoFile(company.LogoPath, server);
                company.LogoPath = null;
                return;
            }

            if (logoFile == null || logoFile.ContentLength <= 0)
            {
                return;
            }

            var previousPath = company.LogoPath;
            company.LogoPath = SaveUploadedLogo(company.Id, logoFile, server);
            DeleteLogoFile(previousPath, server);
        }
    }
}
