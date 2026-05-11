using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Services
{
    /// <summary>
    /// Resolves CV/profile questionnaire-step data stored in <see cref="ApplicantProfile"/> when
    /// multiple <see cref="Applicant"/> rows share the same email within a company (or email casing differs).
    /// </summary>
    public static class ApplicantCvProfileResolution
    {
        public static string NormalizeEmail(string email)
        {
            return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        }

        /// <summary>
        /// Lowercase trimmed email for consistent SQL-side comparisons (EF translates Trim/ToLower for SQL Server).
        /// </summary>
        public static string NormalizedEmailLowerInvariant(string email)
        {
            var normalized = NormalizeEmail(email);
            return normalized == null ? null : normalized.ToLowerInvariant();
        }

        public static List<Applicant> GetApplicantsForCompanyEmail(HrContext context, int companyId, string email)
        {
            var emailKey = NormalizedEmailLowerInvariant(email);
            if (context == null || emailKey == null)
            {
                return new List<Applicant>();
            }

            return context.Applicants
                .Where(a => a.CompanyId == companyId && a.Email != null && a.Email.Trim().ToLower() == emailKey)
                .ToList();
        }

        public static List<Applicant> GetApplicantsForEmailAnyCompany(HrContext context, string email)
        {
            var emailKey = NormalizedEmailLowerInvariant(email);
            if (context == null || emailKey == null)
            {
                return new List<Applicant>();
            }

            return context.Applicants
                .Where(a => a.Email != null && a.Email.Trim().ToLower() == emailKey)
                .ToList();
        }

        public static Applicant SelectBestApplicantForCv(HrContext context, List<Applicant> candidates)
        {
            if (context == null || candidates == null || candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            var ids = candidates.Select(a => a.Id).ToList();
            var profile = context.ApplicantProfiles
                .Where(p => ids.Contains(p.ApplicantId))
                .OrderByDescending(p => p.UpdatedOn)
                .FirstOrDefault();

            if (profile != null)
            {
                return candidates.First(a => a.Id == profile.ApplicantId);
            }

            return candidates.OrderBy(a => a.Id).First();
        }

        public static ApplicantProfile GetLatestProfileForApplicantIds(
            HrContext context,
            ICollection<int> applicantIds,
            bool asNoTracking)
        {
            if (context == null || applicantIds == null || applicantIds.Count == 0)
            {
                return null;
            }

            var query = asNoTracking ? context.ApplicantProfiles.AsNoTracking() : context.ApplicantProfiles;
            return query
                .Where(p => applicantIds.Contains(p.ApplicantId))
                .OrderByDescending(p => p.UpdatedOn)
                .FirstOrDefault();
        }
    }
}
