using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HR.Web.Models
{
    public enum CustomReportDataset
    {
        Applications = 0,
        ApplicantsPerPosition = 1
    }

    public enum CustomReportTimePreset
    {
        AllTime = 0,
        Last7Days = 1,
        Last30Days = 2,
        Last90Days = 3,
        ThisYear = 4,
        Custom = 5
    }

    public enum CustomReportSortDirection
    {
        Asc = 0,
        Desc = 1
    }

    public sealed class CustomReportConfig
    {
        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("dataset")]
        public string Dataset { get; set; }

        [JsonProperty("columns")]
        public List<string> Columns { get; set; }

        [JsonProperty("timeFrame")]
        public CustomReportTimeFrame TimeFrame { get; set; }

        [JsonProperty("filters")]
        public CustomReportFilters Filters { get; set; }

        [JsonProperty("sort")]
        public CustomReportSort Sort { get; set; }
    }

    public sealed class CustomReportTimeFrame
    {
        [JsonProperty("preset")]
        public string Preset { get; set; }

        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }
    }

    public sealed class CustomReportFilters
    {
        [JsonProperty("statuses")]
        public List<string> Statuses { get; set; }

        [JsonProperty("positionIds")]
        public List<int> PositionIds { get; set; }

        [JsonProperty("departmentIds")]
        public List<int> DepartmentIds { get; set; }

        [JsonProperty("openPositionsOnly")]
        public bool OpenPositionsOnly { get; set; }
    }

    public sealed class CustomReportSort
    {
        [JsonProperty("column")]
        public string Column { get; set; }

        [JsonProperty("direction")]
        public string Direction { get; set; }
    }

    public sealed class ValidatedCustomReport
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public CustomReportDataset Dataset { get; set; }
        public IList<string> Columns { get; set; }
        public CustomReportTimePreset Preset { get; set; }
        public DateTime? FromUtcInclusive { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
        public IList<string> Statuses { get; set; }
        public IList<int> PositionIds { get; set; }
        public IList<int> DepartmentIds { get; set; }
        public bool OpenPositionsOnly { get; set; }
        public string SortColumn { get; set; }
        public CustomReportSortDirection SortDirection { get; set; }
        public string ConfigJson { get; set; }
    }

    public sealed class CustomReportDatasetDefinition
    {
        public CustomReportDataset Dataset { get; set; }
        public string WireKey { get; set; }
        public string Label { get; set; }
        public IList<CustomReportColumnDefinition> Columns { get; set; }
    }

    public sealed class CustomReportColumnDefinition
    {
        public string Key { get; set; }
        public string Header { get; set; }
        public bool Required { get; set; }
    }

    public sealed class CustomReportPage
    {
        public IList<string> Headers { get; set; }
        public IList<IList<string>> Rows { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
        public string EmptyMessage { get; set; }
        public string Warning { get; set; }
    }

    public sealed class CustomReportListItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DatasetLabel { get; set; }
        public string UpdatedOnDisplay { get; set; }
        public string CreatedBy { get; set; }
    }

    public sealed class CustomReportListPage
    {
        public IList<CustomReportListItem> Items { get; set; }
        public int Page { get; set; }
        public bool HasMore { get; set; }
    }
}
