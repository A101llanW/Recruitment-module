using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Services
{
    public static class CandidateEmailCcHelper
    {
        public static string ValidateCcToggles(
            bool includePanelistCc,
            IEnumerable<int> selectedPanelistUserIds,
            bool includeHrCc,
            IEnumerable<int> selectedHrContactIds)
        {
            if (!includePanelistCc && !includeHrCc)
            {
                return null;
            }

            if (includePanelistCc)
            {
                if (selectedPanelistUserIds == null || !selectedPanelistUserIds.Any(id => id > 0))
                {
                    return "Select at least one panelist to CC, or turn off CC panelists.";
                }
            }

            if (includeHrCc)
            {
                if (selectedHrContactIds == null || !selectedHrContactIds.Any(id => id > 0))
                {
                    return "Select at least one HR CC address, or turn off CC HR / internal addresses.";
                }
            }

            return null;
        }

        public static bool UserQualifiesAsPanelistCc(User user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            if (string.Equals(user.Role, "Panelist", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) && user.IsPanelist;
        }

        public static List<User> GetPanelistUsersForCc(IEnumerable<User> tenantUsers)
        {
            return (tenantUsers ?? Enumerable.Empty<User>())
                .Where(UserQualifiesAsPanelistCc)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToList();
        }

        public static List<CompanyHrCcEmail> GetActiveHrContacts(HrContext context, int companyId)
        {
            if (context == null)
            {
                return new List<CompanyHrCcEmail>();
            }

            return context.CompanyHrCcEmails
                .AsNoTracking()
                .Where(e => e.CompanyId == companyId && e.IsActive)
                .OrderBy(e => e.SortOrder)
                .ThenBy(e => e.Email)
                .ToList();
        }

        public static List<string> ResolvePanelistEmails(
            IEnumerable<User> companyUsers,
            int companyId,
            IEnumerable<int> selectedUserIds,
            string primaryRecipientEmail = null)
        {
            if (selectedUserIds == null)
            {
                return new List<string>();
            }

            var idSet = new HashSet<int>(selectedUserIds.Where(id => id > 0));
            if (!idSet.Any())
            {
                return new List<string>();
            }

            var recipients = (companyUsers ?? Enumerable.Empty<User>())
                .Where(u =>
                    u.CompanyId == companyId &&
                    idSet.Contains(u.Id) &&
                    UserQualifiesAsPanelistCc(u))
                .Select(u => u.Email.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            RemovePrimary(recipients, primaryRecipientEmail);
            return recipients;
        }

        public static List<string> ResolveHrEmails(
            IEnumerable<CompanyHrCcEmail> companyContacts,
            int companyId,
            IEnumerable<int> selectedIds,
            string primaryRecipientEmail = null)
        {
            if (selectedIds == null || companyContacts == null)
            {
                return new List<string>();
            }

            var idSet = new HashSet<int>(selectedIds.Where(id => id > 0));
            if (!idSet.Any())
            {
                return new List<string>();
            }

            var recipients = companyContacts
                .Where(e => e.CompanyId == companyId && e.IsActive && idSet.Contains(e.Id))
                .Select(e => e.Email.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            RemovePrimary(recipients, primaryRecipientEmail);
            return recipients;
        }

        public static List<string> BuildMergedCandidateCc(
            UnitOfWork uow,
            int companyId,
            bool includePanelistCc,
            IEnumerable<int> selectedPanelistUserIds,
            bool includeHrCc,
            IEnumerable<int> selectedHrContactIds,
            string primaryRecipientEmail)
        {
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }

            var companyUsers = uow.Users.GetAll().Where(u => u.CompanyId == companyId).ToList();
            var hrContacts = GetActiveHrContacts(uow.Context, companyId);
            var panelistEmails = includePanelistCc
                ? ResolvePanelistEmails(companyUsers, companyId, selectedPanelistUserIds, primaryRecipientEmail)
                : new List<string>();
            var hrEmails = includeHrCc
                ? ResolveHrEmails(hrContacts, companyId, selectedHrContactIds, primaryRecipientEmail)
                : new List<string>();

            return MergeCcLists(panelistEmails, hrEmails, primaryRecipientEmail);
        }

        public static List<string> MergeCcLists(
            IEnumerable<string> first,
            IEnumerable<string> second,
            string primaryRecipientEmail = null)
        {
            var merged = new List<string>();
            if (first != null)
            {
                merged.AddRange(first.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
            }

            if (second != null)
            {
                merged.AddRange(second.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
            }

            merged = merged
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            RemovePrimary(merged, primaryRecipientEmail);
            return merged;
        }

        private static void RemovePrimary(List<string> emails, string primaryRecipientEmail)
        {
            if (string.IsNullOrWhiteSpace(primaryRecipientEmail) || emails == null || !emails.Any())
            {
                return;
            }

            var p = primaryRecipientEmail.Trim();
            emails.RemoveAll(e => string.Equals(e, p, StringComparison.OrdinalIgnoreCase));
        }
    }
}
