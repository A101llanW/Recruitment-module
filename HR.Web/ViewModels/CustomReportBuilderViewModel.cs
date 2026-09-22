using System.Collections.Generic;
using HR.Web.Models;

namespace HR.Web.ViewModels
{
    public sealed class CustomReportBuilderViewModel
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DatasetWireKey { get; set; }
        public IList<string> SelectedColumns { get; set; }
        public string Preset { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public IList<string> SelectedStatuses { get; set; }
        public IList<int> SelectedPositionIds { get; set; }
        public IList<int> SelectedDepartmentIds { get; set; }
        public bool OpenPositionsOnly { get; set; }
        public string SortColumn { get; set; }
        public string SortDirection { get; set; }
        public string Error { get; set; }
        public string CompanyWarning { get; set; }
        public bool SavedFlash { get; set; }
        public IList<CustomReportDatasetDefinition> Datasets { get; set; }
        public IList<string> Statuses { get; set; }
        public IList<IdLabel> Positions { get; set; }
        public bool PositionsHasMore { get; set; }
        public IList<IdLabel> Departments { get; set; }
        public bool DepartmentsHasMore { get; set; }
    }

    public sealed class IdLabel
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }

    public sealed class CustomReportRunViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DatasetLabel { get; set; }
        public CustomReportPage Page { get; set; }
        public string Error { get; set; }
    }
}
