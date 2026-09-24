using System;
using System.Collections.Generic;
using System.Linq;
using HR.Web.Models;

namespace HR.Web.Services
{
    public static class CustomReportCatalog
    {
        public const string ApplicationsWireKey = "applications";
        public const string ApplicantsPerPositionWireKey = "applicantsPerPosition";

        public static readonly IList<string> Statuses = new List<string>
        {
            "Interviewing",
            "Offer",
            "Hired",
            "Rejected",
            "Pending",
            "Shortlisted",
            "Interviewed",
            "Approved"
        };

        private static readonly IList<CustomReportColumnDefinition> ApplicationColumns = new List<CustomReportColumnDefinition>
        {
            new CustomReportColumnDefinition { Key = "applicantName", Header = "Applicant" },
            new CustomReportColumnDefinition { Key = "email", Header = "Email" },
            new CustomReportColumnDefinition { Key = "phone", Header = "Phone" },
            new CustomReportColumnDefinition { Key = "positionTitle", Header = "Position" },
            new CustomReportColumnDefinition { Key = "departmentName", Header = "Department" },
            new CustomReportColumnDefinition { Key = "status", Header = "Status" },
            new CustomReportColumnDefinition { Key = "appliedOn", Header = "Applied" },
            new CustomReportColumnDefinition { Key = "score", Header = "Score" },
            new CustomReportColumnDefinition { Key = "workExperienceLevel", Header = "Experience" },
            new CustomReportColumnDefinition { Key = "positionLocation", Header = "Location" },
            new CustomReportColumnDefinition { Key = "isOpen", Header = "Position open" }
        };

        private static readonly IList<CustomReportColumnDefinition> ApplicantsPerPositionColumns = new List<CustomReportColumnDefinition>
        {
            new CustomReportColumnDefinition { Key = "positionTitle", Header = "Position" },
            new CustomReportColumnDefinition { Key = "departmentName", Header = "Department" },
            new CustomReportColumnDefinition { Key = "positionLocation", Header = "Location" },
            new CustomReportColumnDefinition { Key = "isOpen", Header = "Position open" },
            new CustomReportColumnDefinition { Key = "applicantCount", Header = "Applicants", Required = true }
        };

        public static readonly IList<string> ApplicationDefaultColumns = new List<string>
        {
            "applicantName",
            "positionTitle",
            "status",
            "appliedOn"
        };

        public static readonly IList<string> ApplicantsPerPositionDefaultColumns = new List<string>
        {
            "positionTitle",
            "departmentName",
            "applicantCount"
        };

        public static CustomReportDatasetDefinition Get(CustomReportDataset dataset)
        {
            switch (dataset)
            {
                case CustomReportDataset.Applications:
                    return new CustomReportDatasetDefinition
                    {
                        Dataset = CustomReportDataset.Applications,
                        WireKey = ApplicationsWireKey,
                        Label = "Application rows",
                        Columns = ApplicationColumns
                    };
                case CustomReportDataset.ApplicantsPerPosition:
                    return new CustomReportDatasetDefinition
                    {
                        Dataset = CustomReportDataset.ApplicantsPerPosition,
                        WireKey = ApplicantsPerPositionWireKey,
                        Label = "Applicants per position",
                        Columns = ApplicantsPerPositionColumns
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataset), dataset, "Unknown custom report dataset.");
            }
        }

        public static IList<CustomReportDatasetDefinition> GetAllDatasets()
        {
            return new List<CustomReportDatasetDefinition>
            {
                Get(CustomReportDataset.Applications),
                Get(CustomReportDataset.ApplicantsPerPosition)
            };
        }

        public static bool ParseDataset(string wire, out CustomReportDataset dataset)
        {
            if (string.Equals(wire, ApplicationsWireKey, StringComparison.Ordinal))
            {
                dataset = CustomReportDataset.Applications;
                return true;
            }

            if (string.Equals(wire, ApplicantsPerPositionWireKey, StringComparison.Ordinal))
            {
                dataset = CustomReportDataset.ApplicantsPerPosition;
                return true;
            }

            dataset = default(CustomReportDataset);
            return false;
        }

        public static bool ParsePreset(string wire, out CustomReportTimePreset preset)
        {
            switch (wire)
            {
                case "allTime":
                    preset = CustomReportTimePreset.AllTime;
                    return true;
                case "last7Days":
                    preset = CustomReportTimePreset.Last7Days;
                    return true;
                case "last30Days":
                    preset = CustomReportTimePreset.Last30Days;
                    return true;
                case "last90Days":
                    preset = CustomReportTimePreset.Last90Days;
                    return true;
                case "thisYear":
                    preset = CustomReportTimePreset.ThisYear;
                    return true;
                case "custom":
                    preset = CustomReportTimePreset.Custom;
                    return true;
                default:
                    preset = default(CustomReportTimePreset);
                    return false;
            }
        }

        public static string GetPresetWireKey(CustomReportTimePreset preset)
        {
            switch (preset)
            {
                case CustomReportTimePreset.AllTime:
                    return "allTime";
                case CustomReportTimePreset.Last7Days:
                    return "last7Days";
                case CustomReportTimePreset.Last30Days:
                    return "last30Days";
                case CustomReportTimePreset.Last90Days:
                    return "last90Days";
                case CustomReportTimePreset.ThisYear:
                    return "thisYear";
                case CustomReportTimePreset.Custom:
                    return "custom";
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown time preset.");
            }
        }

        public static bool ParseSortDirection(string wire, out CustomReportSortDirection direction)
        {
            if (string.Equals(wire, "asc", StringComparison.OrdinalIgnoreCase))
            {
                direction = CustomReportSortDirection.Asc;
                return true;
            }

            if (string.Equals(wire, "desc", StringComparison.OrdinalIgnoreCase))
            {
                direction = CustomReportSortDirection.Desc;
                return true;
            }

            direction = default(CustomReportSortDirection);
            return false;
        }

        public static string GetSortDirectionWireKey(CustomReportSortDirection direction)
        {
            switch (direction)
            {
                case CustomReportSortDirection.Asc:
                    return "asc";
                case CustomReportSortDirection.Desc:
                    return "desc";
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown sort direction.");
            }
        }

        public static bool IsColumnInDataset(CustomReportDataset dataset, string columnKey)
        {
            return Get(dataset).Columns.Any(c => string.Equals(c.Key, columnKey, StringComparison.Ordinal));
        }

        public static string GetColumnHeader(CustomReportDataset dataset, string columnKey)
        {
            var column = Get(dataset).Columns.FirstOrDefault(c => string.Equals(c.Key, columnKey, StringComparison.Ordinal));
            return column != null ? column.Header : columnKey;
        }
    }
}
