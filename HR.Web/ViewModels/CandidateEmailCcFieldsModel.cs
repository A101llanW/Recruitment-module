using System;
using System.Collections.Generic;
using HR.Web.Models;

namespace HR.Web.ViewModels
{
    public class CandidateEmailCcFieldsModel
    {
        public string Prefix { get; set; }

        public List<User> Panelists { get; set; }

        public List<CompanyHrCcEmail> HrContacts { get; set; }

        public Uri ManageHrCcUrl { get; set; }
    }
}
