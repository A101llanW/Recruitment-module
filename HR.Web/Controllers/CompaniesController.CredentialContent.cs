using System;
using System.Collections.Generic;
using System.Text;
using HR.Web.Models;

namespace HR.Web.Controllers
{
    public partial class CompaniesController
    {
        private byte[] BuildCredentialFileContent(AdminCredentialsViewModel data, TemporaryCredential credential)
        {
            var lines = new List<string>
            {
                "=================================================",
                "   HR SYSTEM - SECURE ADMIN CREDENTIALS",
                "=================================================",
                string.Empty,
                "Company Name:   " + data.CompanyName,
                "Login URL:      " + data.CompanyUrl,
                "Admin Username: " + data.AdminUsername,
                "Temp Password:  " + data.AdminPassword,
                string.Empty,
                "-------------------------------------------------",
                "Generated on:   " + credential.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                "Downloaded by:  " + BuildCredentialDownloadedByText(),
                "-------------------------------------------------",
                string.Empty,
                "SECURITY WARNING:",
                "1. This is a ONE-TIME download link and has now been invalidated.",
                "2. Store this file in a secure location (e.g., password manager).",
                "3. The administrator should change their password upon first login.",
                "================================================="
            };

            return Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
        }

        private string BuildCredentialDownloadedByText()
        {
            return User.Identity != null && User.Identity.IsAuthenticated
                ? User.Identity.Name
                : "Anonymous (Via Secure Link)";
        }
    }
}
