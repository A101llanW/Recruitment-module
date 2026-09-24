using System;

namespace HR.Web.ViewModels
{
    public class PositionCandidateActionViewModel
    {
        public string Label { get; set; }
        public string Url { get; set; }
        public string IconClass { get; set; }
        public bool ShowPrimaryAction { get; set; } = true;
        public bool HasApplied { get; set; }
        public bool ShowRecentlyViewed { get; set; }
        public string BadgeLabel { get; set; }
        public string BadgeCssClass { get; set; }
        public DateTime? AppliedOn { get; set; }
    }
}