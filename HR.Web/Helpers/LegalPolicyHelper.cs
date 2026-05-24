using System;
using System.Collections.Generic;
using HR.Web.Models;

namespace HR.Web.Helpers
{
    /// <summary>
    /// Legal relationship for policy text and stored acceptance versions (applicant vs company admin vs Nanosoft operator).
    /// </summary>
    public enum LegalRelationshipKind
    {
        Applicant,
        CompanyAdmin,
        SuperAdmin
    }

    public static class LegalPolicyHelper
    {
        private static readonly Dictionary<string, LegalRelationshipKind> RelationshipTokens =
            new Dictionary<string, LegalRelationshipKind>(StringComparer.OrdinalIgnoreCase)
            {
                { "applicant", LegalRelationshipKind.Applicant },
                { "companyadmin", LegalRelationshipKind.CompanyAdmin },
                { "company_admin", LegalRelationshipKind.CompanyAdmin },
                { "admin", LegalRelationshipKind.CompanyAdmin },
                { "superadmin", LegalRelationshipKind.SuperAdmin },
                { "super_admin", LegalRelationshipKind.SuperAdmin },
                { "operator", LegalRelationshipKind.SuperAdmin }
            };

        public const string CompanyName = "Nanosoft";
        public const string ContactEmail = "nanosoft.africa@gmail.com";
        public const string ContactAddress = "Nairobi, Kenya";

        public const string ApplicantPrivacyVersion = "2026-05-15-A";
        public const string ApplicantTermsVersion = "2026-05-15-A";

        public const string CompanyAdminPrivacyVersion = "2026-05-15-C";
        public const string CompanyAdminTermsVersion = "2026-05-15-C";

        public const string SuperAdminPrivacyVersion = "2026-05-15-S";
        public const string SuperAdminTermsVersion = "2026-05-15-S";

        public static string GetPrivacyVersion(LegalRelationshipKind relationship)
        {
            switch (relationship)
            {
                case LegalRelationshipKind.CompanyAdmin:
                    return CompanyAdminPrivacyVersion;
                case LegalRelationshipKind.SuperAdmin:
                    return SuperAdminPrivacyVersion;
                default:
                    return ApplicantPrivacyVersion;
            }
        }

        public static string GetTermsVersion(LegalRelationshipKind relationship)
        {
            switch (relationship)
            {
                case LegalRelationshipKind.CompanyAdmin:
                    return CompanyAdminTermsVersion;
                case LegalRelationshipKind.SuperAdmin:
                    return SuperAdminTermsVersion;
                default:
                    return ApplicantTermsVersion;
            }
        }

        /// <summary>
        /// Determines which policy set applies to this authenticated staff/platform user account.
        /// </summary>
        public static LegalRelationshipKind ResolveUserLegalRelationship(User user)
        {
            if (user == null)
            {
                return LegalRelationshipKind.Applicant;
            }

            var rawRole = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
            var isSuperAdmin = !user.CompanyId.HasValue &&
                               (string.Equals(rawRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(rawRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase));
            if (isSuperAdmin)
            {
                return LegalRelationshipKind.SuperAdmin;
            }

            if (string.Equals(rawRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return LegalRelationshipKind.CompanyAdmin;
            }

            return LegalRelationshipKind.Applicant;
        }

        public static LegalRelationshipKind? TryParseRelationshipToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            LegalRelationshipKind kind;
            return RelationshipTokens.TryGetValue(token.Trim(), out kind) ? (LegalRelationshipKind?)kind : null;
        }

        public static string ToRelationshipQueryValue(LegalRelationshipKind relationship)
        {
            switch (relationship)
            {
                case LegalRelationshipKind.CompanyAdmin:
                    return "companyadmin";
                case LegalRelationshipKind.SuperAdmin:
                    return "superadmin";
                default:
                    return "applicant";
            }
        }

        public static string GetRelationshipDisplayName(LegalRelationshipKind relationship)
        {
            switch (relationship)
            {
                case LegalRelationshipKind.CompanyAdmin:
                    return "company administrator";
                case LegalRelationshipKind.SuperAdmin:
                    return "Nanosoft platform operator (SuperAdmin)";
                default:
                    return "applicant / candidate";
            }
        }

        public static void ApplyUserAcceptance(User user, DateTime acceptedAtUtc, LegalRelationshipKind relationship)
        {
            if (user == null)
            {
                return;
            }

            var pv = GetPrivacyVersion(relationship);
            var tv = GetTermsVersion(relationship);
            if (!user.PrivacyAcceptedAt.HasValue || !string.Equals(user.PrivacyVersion, pv, StringComparison.Ordinal))
            {
                user.PrivacyAcceptedAt = acceptedAtUtc;
                user.PrivacyVersion = pv;
            }

            if (!user.TermsAcceptedAt.HasValue || !string.Equals(user.TermsVersion, tv, StringComparison.Ordinal))
            {
                user.TermsAcceptedAt = acceptedAtUtc;
                user.TermsVersion = tv;
            }
        }

        public static void ApplyApplicantAcceptance(Applicant applicant, DateTime acceptedAtUtc)
        {
            if (applicant == null)
            {
                return;
            }

            var pv = GetPrivacyVersion(LegalRelationshipKind.Applicant);
            var tv = GetTermsVersion(LegalRelationshipKind.Applicant);
            if (!applicant.PrivacyAcceptedAt.HasValue || !string.Equals(applicant.PrivacyVersion, pv, StringComparison.Ordinal))
            {
                applicant.PrivacyAcceptedAt = acceptedAtUtc;
                applicant.PrivacyVersion = pv;
            }

            if (!applicant.TermsAcceptedAt.HasValue || !string.Equals(applicant.TermsVersion, tv, StringComparison.Ordinal))
            {
                applicant.TermsAcceptedAt = acceptedAtUtc;
                applicant.TermsVersion = tv;
            }
        }

        public static bool UserMeetsCurrentPolicyVersions(User user, LegalRelationshipKind relationship)
        {
            if (user == null)
            {
                return false;
            }

            var pv = GetPrivacyVersion(relationship);
            var tv = GetTermsVersion(relationship);
            return user.PrivacyAcceptedAt.HasValue &&
                   user.TermsAcceptedAt.HasValue &&
                   string.Equals(user.PrivacyVersion, pv, StringComparison.Ordinal) &&
                   string.Equals(user.TermsVersion, tv, StringComparison.Ordinal);
        }
    }
}
