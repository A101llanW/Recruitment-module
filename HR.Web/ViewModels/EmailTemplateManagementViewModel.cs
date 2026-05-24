using System.Collections.Generic;
using System.Web.Mvc;

namespace HR.Web.ViewModels
{
    public class EmailTemplateManagementViewModel
    {
        public List<EmailTemplateEditorItemViewModel> Templates { get; set; }
    }

    public class EmailTemplateEditorItemViewModel
    {
        public string TemplateKey { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string TokenHint { get; set; }

        [AllowHtml]
        public string DefaultSubjectTemplate { get; set; }

        [AllowHtml]
        public string DefaultBodyTemplate { get; set; }
    }
}
