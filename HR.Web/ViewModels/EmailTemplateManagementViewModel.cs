using System.Collections.Generic;

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

        public string DefaultSubjectTemplate { get; set; }
        public string DefaultBodyTemplate { get; set; }
    }
}
