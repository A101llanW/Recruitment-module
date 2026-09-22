using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HR.Web.Models;
using Newtonsoft.Json;

namespace HR.Web.Services
{
    public static class CustomReportConfigValidator
    {
        private const int MaxConfigJsonLength = 8000;
        private const int MaxColumns = 12;
        private const int MaxStatuses = 20;
        private const int MaxPositionIds = 50;
        private const int MaxDepartmentIds = 50;
        private const int MaxCustomSpanDays = 3660;

        public static bool TryValidate(
            string name,
            string description,
            string configJson,
            Func<IList<int>, bool> positionsBelongToCompany,
            Func<IList<int>, bool> departmentsBelongToCompany,
            out ValidatedCustomReport config,
            out string error)
        {
            config = null;
            error = null;

            var trimmedName = (name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedName))
            {
                error = "Report name is required.";
                return false;
            }

            if (trimmedName.Length > 100)
            {
                error = "Report name must be 100 characters or fewer.";
                return false;
            }

            var trimmedDescription = (description ?? string.Empty).Trim();
            if (trimmedDescription.Length > 500)
            {
                error = "Description must be 500 characters or fewer.";
                return false;
            }

            if (string.IsNullOrEmpty(configJson))
            {
                error = "The report configuration is not valid.";
                return false;
            }

            if (configJson.Length > MaxConfigJsonLength)
            {
                error = "The report configuration is not valid.";
                return false;
            }

            CustomReportConfig parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<CustomReportConfig>(configJson);
            }
            catch (JsonException)
            {
                error = "The report configuration is not valid.";
                return false;
            }

            if (parsed == null || parsed.Version != 1)
            {
                error = "The report configuration is not valid.";
                return false;
            }

            CustomReportDataset dataset;
            if (!CustomReportCatalog.ParseDataset(parsed.Dataset, out dataset))
            {
                error = "The report configuration is not valid.";
                return false;
            }

            var datasetDefinition = CustomReportCatalog.Get(dataset);
            var columns = NormalizeStringList(parsed.Columns);
            if (columns.Count == 0 || columns.Count > MaxColumns)
            {
                error = "Select between 1 and 12 columns.";
                return false;
            }

            if (columns.Count != columns.Distinct(StringComparer.Ordinal).Count())
            {
                error = "Duplicate columns are not allowed.";
                return false;
            }

            foreach (var column in columns)
            {
                if (!CustomReportCatalog.IsColumnInDataset(dataset, column))
                {
                    error = "The report configuration is not valid.";
                    return false;
                }
            }

            if (dataset == CustomReportDataset.ApplicantsPerPosition && !columns.Contains("applicantCount"))
            {
                error = "Applicants per position reports must include the Applicants column.";
                return false;
            }

            if (parsed.TimeFrame == null || string.IsNullOrEmpty(parsed.TimeFrame.Preset))
            {
                error = "The report configuration is not valid.";
                return false;
            }

            CustomReportTimePreset preset;
            if (!CustomReportCatalog.ParsePreset(parsed.TimeFrame.Preset, out preset))
            {
                error = "The report configuration is not valid.";
                return false;
            }

            DateTime? fromUtcInclusive = null;
            DateTime? toUtcExclusive = null;
            if (preset == CustomReportTimePreset.Custom)
            {
                if (!TryParseCustomRange(parsed.TimeFrame.From, parsed.TimeFrame.To, out fromUtcInclusive, out toUtcExclusive, out error))
                {
                    return false;
                }
            }
            else
            {
                ResolvePresetRange(preset, out fromUtcInclusive, out toUtcExclusive);
            }

            var statuses = NormalizeStringList(parsed.Filters != null ? parsed.Filters.Statuses : null);
            if (statuses.Count > MaxStatuses)
            {
                error = "Too many status filters selected.";
                return false;
            }

            foreach (var status in statuses)
            {
                if (!CustomReportCatalog.Statuses.Contains(status, StringComparer.Ordinal))
                {
                    error = "The report configuration is not valid.";
                    return false;
                }
            }

            var positionIds = NormalizeIntList(parsed.Filters != null ? parsed.Filters.PositionIds : null);
            if (positionIds.Count > MaxPositionIds)
            {
                error = "Too many position filters selected.";
                return false;
            }

            var departmentIds = NormalizeIntList(parsed.Filters != null ? parsed.Filters.DepartmentIds : null);
            if (departmentIds.Count > MaxDepartmentIds)
            {
                error = "Too many department filters selected.";
                return false;
            }

            if (positionIds.Count > 0 && (positionsBelongToCompany == null || !positionsBelongToCompany(positionIds)))
            {
                error = "One or more selected positions are not valid for your company.";
                return false;
            }

            if (departmentIds.Count > 0 && (departmentsBelongToCompany == null || !departmentsBelongToCompany(departmentIds)))
            {
                error = "One or more selected departments are not valid for your company.";
                return false;
            }

            if (parsed.Sort == null || string.IsNullOrEmpty(parsed.Sort.Column) || string.IsNullOrEmpty(parsed.Sort.Direction))
            {
                error = "The report configuration is not valid.";
                return false;
            }

            if (!columns.Contains(parsed.Sort.Column))
            {
                error = "Sort column must be one of the selected columns.";
                return false;
            }

            CustomReportSortDirection sortDirection;
            if (!CustomReportCatalog.ParseSortDirection(parsed.Sort.Direction, out sortDirection))
            {
                error = "The report configuration is not valid.";
                return false;
            }

            var storedConfig = new CustomReportConfig
            {
                Version = 1,
                Dataset = datasetDefinition.WireKey,
                Columns = columns.ToList(),
                TimeFrame = new CustomReportTimeFrame
                {
                    Preset = CustomReportCatalog.GetPresetWireKey(preset),
                    From = preset == CustomReportTimePreset.Custom ? parsed.TimeFrame.From : null,
                    To = preset == CustomReportTimePreset.Custom ? parsed.TimeFrame.To : null
                },
                Filters = new CustomReportFilters
                {
                    Statuses = statuses.ToList(),
                    PositionIds = positionIds.ToList(),
                    DepartmentIds = departmentIds.ToList(),
                    OpenPositionsOnly = parsed.Filters != null && parsed.Filters.OpenPositionsOnly
                },
                Sort = new CustomReportSort
                {
                    Column = parsed.Sort.Column,
                    Direction = CustomReportCatalog.GetSortDirectionWireKey(sortDirection)
                }
            };

            config = new ValidatedCustomReport
            {
                Name = trimmedName,
                Description = string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription,
                Dataset = dataset,
                Columns = columns,
                Preset = preset,
                FromUtcInclusive = fromUtcInclusive,
                ToUtcExclusive = toUtcExclusive,
                Statuses = statuses,
                PositionIds = positionIds,
                DepartmentIds = departmentIds,
                OpenPositionsOnly = parsed.Filters != null && parsed.Filters.OpenPositionsOnly,
                SortColumn = parsed.Sort.Column,
                SortDirection = sortDirection,
                ConfigJson = JsonConvert.SerializeObject(storedConfig)
            };

            return true;
        }

        private static bool TryParseCustomRange(
            string from,
            string to,
            out DateTime? fromUtcInclusive,
            out DateTime? toUtcExclusive,
            out string error)
        {
            fromUtcInclusive = null;
            toUtcExclusive = null;
            error = null;

            DateTime fromDate;
            DateTime toDate;
            if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate) ||
                !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate))
            {
                error = "Custom date range requires valid from and to dates.";
                return false;
            }

            if (fromDate > toDate)
            {
                error = "The from date must be on or before the to date.";
                return false;
            }

            var spanDays = (toDate - fromDate).TotalDays;
            if (spanDays > MaxCustomSpanDays)
            {
                error = "Custom date range cannot exceed 3660 days.";
                return false;
            }

            fromUtcInclusive = fromDate.Date;
            toUtcExclusive = toDate.Date.AddDays(1);
            return true;
        }

        private static void ResolvePresetRange(
            CustomReportTimePreset preset,
            out DateTime? fromUtcInclusive,
            out DateTime? toUtcExclusive)
        {
            fromUtcInclusive = null;
            toUtcExclusive = null;

            var today = DateTime.UtcNow.Date;
            switch (preset)
            {
                case CustomReportTimePreset.AllTime:
                    return;
                case CustomReportTimePreset.Last7Days:
                    fromUtcInclusive = today.AddDays(-7);
                    toUtcExclusive = today.AddDays(1);
                    return;
                case CustomReportTimePreset.Last30Days:
                    fromUtcInclusive = today.AddDays(-30);
                    toUtcExclusive = today.AddDays(1);
                    return;
                case CustomReportTimePreset.Last90Days:
                    fromUtcInclusive = today.AddDays(-90);
                    toUtcExclusive = today.AddDays(1);
                    return;
                case CustomReportTimePreset.ThisYear:
                    fromUtcInclusive = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    toUtcExclusive = new DateTime(DateTime.UtcNow.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    return;
                case CustomReportTimePreset.Custom:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown time preset.");
            }
        }

        private static List<string> NormalizeStringList(IList<string> values)
        {
            if (values == null)
            {
                return new List<string>();
            }

            return values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .ToList();
        }

        private static List<int> NormalizeIntList(IList<int> values)
        {
            if (values == null)
            {
                return new List<int>();
            }

            return values
                .Where(v => v > 0)
                .Distinct()
                .ToList();
        }
    }
}
