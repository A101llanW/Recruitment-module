using HR.Web.Models;

namespace HR.Web.ViewModels
{
    public sealed class CustomReportPageViewModel
    {
        public CustomReportListPage Saved { get; set; }
        public bool CanManage { get; set; }
    }
}
