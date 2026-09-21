using System.Web.Mvc;

namespace HR.Web.ViewModels
{
    /// <summary>
    /// POST binding for interview and failed-candidate email send actions.
    /// [AllowHtml] is scoped to Body only so HTML from TinyMCE passes request validation.
    /// </summary>
    public class CandidateEmailSendForm
    {
        public int ApplicationId { get; set; }

        public int PositionId { get; set; }

        public string Subject { get; set; }

        [AllowHtml]
        public string Body { get; set; }

        public string ComposeMode { get; set; }

        public string TemplateKey { get; set; }

        public bool IncludePanelistCc { get; set; }

        public bool IncludeHrCc { get; set; }

        public int[] SelectedPanelistIds { get; set; }

        public int[] SelectedHrCcIds { get; set; }
    }
}