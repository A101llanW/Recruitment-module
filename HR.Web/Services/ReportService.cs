using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Services
{
    public class ReportService
    {
        private readonly UnitOfWork _uow;
        private readonly TenantService _tenantService;

        public ReportService()
        {
            _uow = new UnitOfWork();
            _tenantService = new TenantService(_uow);
        }

        public string GenerateReportByType(string reportType, string generatedBy, string format = "csv")
        {
            string filePath = "";
            string html = "";
            var fileName = string.Format("{0}_{1:yyyyMMdd_HHmmss}.{2}", reportType, DateTime.Now, format.ToLower());
            filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            switch (reportType.ToLower())
            {
                case "candidate":
                    if (format.ToLower() == "pdf") html = GenerateCandidatePDF(generatedBy);
                    else 
                    {
                        var q = _uow.Applicants.GetAll().AsQueryable();
                        q = _tenantService.ApplyTenantFilter(q);
                        GenerateCandidateCSV(q.ToList(), filePath);
                    }
                    break;
                case "application":
                    if (format.ToLower() == "pdf") html = GenerateApplicationPDF(generatedBy);
                    else 
                    {
                        var q = _uow.Applications.GetAll(a => a.Applicant, a => a.Position).AsQueryable();
                        q = _tenantService.ApplyTenantFilter(q);
                        GenerateApplicationCSV(q.ToList(), filePath);
                    }
                    break;
                case "interview":
                    if (format.ToLower() == "pdf") html = GenerateInterviewPDF(generatedBy);
                    else 
                    {
                        var q = _uow.Interviews.GetAll().AsQueryable();
                        q = _tenantService.ApplyTenantFilter(q);
                        GenerateInterviewCSV(q.ToList(), filePath);
                    }
                    break;
                case "department":
                    if (format.ToLower() == "pdf") html = GenerateDepartmentPDF(generatedBy);
                    else 
                    {
                        var q = _uow.Departments.GetAll(d => d.Positions).AsQueryable();
                        q = _tenantService.ApplyTenantFilter(q);
                        GenerateDepartmentCSV(q.ToList(), filePath);
                    }
                    break;
                case "position":
                    if (format.ToLower() == "pdf") html = GeneratePositionPDF(generatedBy);
                    else 
                    {
                        var q = _uow.Positions.GetAll(p => p.Department).AsQueryable();
                        q = _tenantService.ApplyTenantFilter(q);
                        GeneratePositionCSV(q.ToList(), filePath);
                    }
                    break;
                case "security":
                    if (format.ToLower() == "pdf") html = GenerateSecurityPDF(generatedBy);
                    else 
                    {
                        var q = _uow.AuditLogs.GetAll().AsQueryable();
                        q = _tenantService.ApplyTenantFilter(q);
                        GenerateSecurityCSV(q.ToList(), filePath);
                    }
                    break;
                default:
                    throw new ArgumentException("Unsupported report type: " + reportType);
            }

            if (format.ToLower() == "pdf" && !string.IsNullOrEmpty(html))
            {
                File.WriteAllText(filePath, html);
            }

            return filePath;
        }

        public string PreviewReportByType(string reportType, string generatedBy)
        {
            switch (reportType.ToLower())
            {
                case "candidate": return GenerateCandidatePDF(generatedBy);
                case "application": return GenerateApplicationPDF(generatedBy);
                case "interview": return GenerateInterviewPDF(generatedBy);
                case "department": return GenerateDepartmentPDF(generatedBy);
                case "position": return GeneratePositionPDF(generatedBy);
                case "security": return GenerateSecurityPDF(generatedBy);
                default: return "<h3>Report type not supported for preview</h3>";
            }
        }

        private string GetReportStyles(string themeColor = "#3498db", string secondaryColor = "#2980b9")
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(@"
        @page {
            margin: 1.5cm;
            size: A4;
        }
        body { 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            margin: 0;
            padding: 0;
            line-height: 1.6;
            color: #2c3e50;
            background-color: #fff;
        }
        .container {
            padding: 20px;
        }
        .report-frame {
            border: 1px solid #e1e8ed;
            border-radius: 20px;
            margin: 0 0 30px 0;
            padding: 0;
            background: #fff;
            box-shadow: 0 5px 15px rgba(0,0,0,0.03);
            border-top: 5px solid ");
            sb.Append(themeColor);
            sb.Append(@";
            display: block;
            width: 100%;
        }
        .report-header-box {
            text-align: center;
            background: ");
            sb.Append(themeColor);
            sb.Append(@";
            color: white;
            padding: 30px 20px;
            margin-bottom: 20px;
        }
        .report-header-box h1 {
            margin: 0;
            font-size: 28px;
            font-weight: 600;
            text-transform: uppercase;
        }
        .report-header-box p {
            margin: 5px 0 0 0;
            opacity: 0.9;
            font-size: 15px;
        }
        .report-meta {
            display: table;
            width: 100%;
            background: #f8f9fa;
            padding: 15px;
            border-radius: 10px;
            margin: 0 40px 20px 40px;
            width: calc(100% - 80px);
            border-left: 5px solid ");
            sb.Append(themeColor);
            sb.Append(@";
        }
        .meta-item {
            display: table-cell;
            width: 33.33%;
        }
        .meta-label {
            font-size: 11px;
            color: #7f8c8d;
            text-transform: uppercase;
            font-weight: bold;
            display: block;
        }
        .meta-value {
            font-size: 14px;
            color: #2c3e50;
            font-weight: 600;
        }
        .report-table { 
            border-collapse: collapse;
            width: 100%; 
            margin-bottom: 30px;
        }
        .report-table thead {
            display: table-header-group;
        }
        .report-table th { 
            background-color: ");
            sb.Append(themeColor);
            sb.Append(@";
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 600;
            font-size: 12px;
            text-transform: uppercase;
        }
        .report-table td { 
            border-bottom: 1px solid #f1f4f6;
            padding: 12px 15px;
            font-size: 13px;
        }
        .report-table tr {
            page-break-inside: avoid;
        }
        .status-badge {
            padding: 4px 10px;
            border-radius: 15px;
            font-size: 10px;
            font-weight: bold;
            text-transform: uppercase;
            display: inline-block;
        }
        .badge-success { background: #d4edda; color: #155724; }
        .badge-warning { background: #fff3cd; color: #856404; }
        .badge-danger { background: #f8d7da; color: #721c24; }
        
        .report-footer { 
            text-align: center;
            font-size: 10px;
            color: #95a5a6;
            padding: 15px 0;
            width: 100%;
        }
        
        @media print {
            .report-footer {
                position: fixed;
                bottom: 0;
                left: 0;
                background: white;
                border-top: 1px solid #eee;
            }
            .container { padding-bottom: 60px; }
        }
        
        .stats-grid {
            display: table;
            width: calc(100% - 80px);
            margin: 0 40px 20px 40px;
            border-spacing: 15px;
        }
        .stat-card-cell {
            display: table-cell;
            background: #fff;
            padding: 15px;
            border-radius: 12px;
            text-align: center;
            border: 1px solid #ecf0f1;
            width: 25%;
        }
        .stat-value {
            font-size: 24px;
            font-weight: 700;
            color: ");
            sb.Append(themeColor);
            sb.Append(@";
            display: block;
        }
        .stat-label {
            font-size: 11px;
            color: #7f8c8d;
            text-transform: uppercase;
            font-weight: bold;
        }
");
            return sb.ToString();
        }

        private string GetReportHeader(string title, string subtitle, string themeColor = "#3498db", string secondaryColor = "#2980b9")
        {
            return string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <title>{0}</title>
    <style>{1}</style>
</head>
<body>
    <div class='container'>", title, GetReportStyles(themeColor, secondaryColor));
        }

        private string GetReportMeta(string generatedBy, string reportCode)
        {
            return string.Format(@"
        <div class='report-meta'>
            <div class='meta-item'>
                <span class='meta-label'>Generated By</span>
                <span class='meta-value'>{0}</span>
            </div>
            <div class='meta-item'>
                <span class='meta-label'>Date generated</span>
                <span class='meta-value'>{1:MMMM dd, yyyy HH:mm}</span>
            </div>
            <div class='meta-item'>
                <span class='meta-label'>Report ID</span>
                <span class='meta-value'>{2}</span>
            </div>
        </div>", generatedBy, DateTime.Now, reportCode);
        }

        private string GetPageFooter()
        {
            return string.Format(@"
        <div class='report-footer'>
            <p><strong>Recruitment Management System</strong></p>
            <p>This is a system-generated confidential document.</p>
            <p>&copy; {0} Nanosoft Technologies. All rights reserved.</p>
        </div>", DateTime.Now.Year);
        }

        private string GetReportFooter()
        {
            return @"
    </div>
</body>
</html>";
        }

        private void GenerateCandidateCSV(List<Applicant> candidates, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("ID,FullName,Email,Phone");
                foreach (var candidate in candidates)
                {
                    writer.WriteLine(string.Format("{0},{1},{2},{3}", candidate.Id, candidate.FullName, candidate.Email, candidate.Phone));
                }
            }
        }

        private string GenerateCandidatePDF(string generatedBy)
        {
            var query = _uow.Applicants.GetAll().AsQueryable();
            query = _tenantService.ApplyTenantFilter(query);
            var candidates = query.ToList();
            var html = GetReportHeader("Candidates Report", "Detailed analysis of applicant profiles", "#3498db", "#2980b9");
            
            html += "<div class='report-frame'>";
            html += @"<div class='report-header-box' style='background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);'>
                        <h1>Candidates Report</h1>
                        <p>Detailed analysis of applicant profiles</p>
                    </div>";
            html += GetReportMeta(generatedBy, "RPT-CAND-" + DateTime.Now.ToString("yyyyMMdd"));
            html += string.Format(@"
                <div class='stats-grid'>
                    <div class='stat-card-cell'>
                        <span class='stat-value'>{0}</span>
                        <span class='stat-label'>Total Candidates</span>
                    </div>
                    <div class='stat-card-cell'>
                        <span class='stat-value'>{1}</span>
                        <span class='stat-label'>With Email</span>
                    </div>
                </div>", candidates.Count, candidates.Count(c => !string.IsNullOrEmpty(c.Email)));

            html += @"<div style='padding: 0 40px 40px 40px;'>
                        <table class='report-table'>
                            <thead>
                                <tr>
                                    <th>ID</th>
                                    <th>Full Name</th>
                                    <th>Email</th>
                                    <th>Phone</th>
                                </tr>
                            </thead>
                            <tbody>";

            foreach (var c in candidates)
            {
                html += string.Format(@"
                <tr>
                    <td><strong>#{0}</strong></td>
                    <td>{1}</td>
                    <td>{2}</td>
                    <td>{3}</td>
                </tr>", c.Id, c.FullName, c.Email, c.Phone);
            }

            html += "</tbody></table></div></div>" + GetPageFooter() + GetReportFooter();
            return html;
        }

        private void GenerateApplicationCSV(List<Application> applications, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("ID,Applicant,Position,Status,AppliedDate,Score");
                foreach (var app in applications)
                {
                    writer.WriteLine(string.Format("{0},{1},{2},{3},{4:yyyy-MM-dd},{5}", app.Id, app.Applicant.FullName, app.Position.Title, app.Status, app.AppliedOn, app.Score));
                }
            }
        }

        private string GenerateApplicationPDF(string generatedBy)
        {
            var query = _uow.Applications.GetAll(a => a.Applicant, a => a.Position).AsQueryable();
            query = _tenantService.ApplyTenantFilter(query);
            var applications = query.ToList();
            var html = GetReportHeader("Applications Report", "Track job application statuses and performance", "#e74c3c", "#c0392b");
            
            html += "<div class='report-frame'>";
            html += @"<div class='report-header-box' style='background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);'>
                        <h1>Applications Report</h1>
                        <p>Track job application statuses and performance</p>
                    </div>";
            html += GetReportMeta(generatedBy, "RPT-APP-" + DateTime.Now.ToString("yyyyMMdd"));
            html += string.Format(@"
                <div class='stats-grid'>
                    <div class='stat-card-cell'>
                        <span class='stat-value' style='color:#e74c3c'>{0}</span>
                        <span class='stat-label'>Total Apps</span>
                    </div>
                    <div class='stat-card-cell'>
                        <span class='stat-value' style='color:#e74c3c'>{1}</span>
                        <span class='stat-label'>Approved</span>
                    </div>
                    <div class='stat-card-cell'>
                        <span class='stat-value' style='color:#e74c3c'>{2}</span>
                        <span class='stat-label'>Pending</span>
                    </div>
                </div>", applications.Count, applications.Count(a => a.Status == "Approved"), applications.Count(a => a.Status == "Pending"));

            html += @"<div style='padding: 0 40px 40px 40px;'>
                        <table class='report-table'>
                            <thead>
                                <tr style='background-color:#e74c3c'>
                                    <th>ID</th>
                                    <th>Applicant</th>
                                    <th>Position</th>
                                    <th>Status</th>
                                    <th>Date</th>
                                    <th>Score</th>
                                </tr>
                            </thead>
                            <tbody>";

            foreach (var a in applications)
            {
                var badge = "badge-warning";
                if (a.Status == "Approved") badge = "badge-success";
                else if (a.Status == "Rejected") badge = "badge-danger";

                html += string.Format(@"
                <tr>
                    <td><strong>#{0}</strong></td>
                    <td>{1}</td>
                    <td>{2}</td>
                    <td><span class='status-badge {3}'>{4}</span></td>
                    <td>{5:yyyy-MM-dd}</td>
                    <td>{6}</td>
                </tr>", a.Id, (a.Applicant != null ? a.Applicant.FullName : "N/A"), (a.Position != null ? a.Position.Title : "N/A"), badge, a.Status, a.AppliedOn, a.Score);
            }

            html += "</tbody></table>" + GetPageFooter() + "</div></div>" + GetReportFooter();
            return html;
        }

        private void GenerateInterviewCSV(List<Interview> interviews, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("ID,ApplicationID,InterviewerID,ScheduledDate,Mode,Notes");
                foreach (var interview in interviews)
                {
                    writer.WriteLine(string.Format("{0},{1},{2},{3:yyyy-MM-dd HH:mm},{4},{5}", interview.Id, interview.ApplicationId, interview.InterviewerId, interview.ScheduledAt, interview.Mode, interview.Notes));
                }
            }
        }

        private string GenerateInterviewPDF(string generatedBy)
        {
            var query = _uow.Interviews.GetAll().AsQueryable();
            query = _tenantService.ApplyTenantFilter(query);
            var interviews = query.ToList();
            var html = GetReportHeader("Interviews Report", "Scheduled candidate assessments and feedback", "#9b59b6", "#8e44ad");
            
            html += "<div class='report-frame'>";
            html += @"<div class='report-header-box' style='background: linear-gradient(135deg, #9b59b6 0%, #8e44ad 100%);'>
                        <h1>Interviews Report</h1>
                        <p>Scheduled candidate assessments and feedback</p>
                    </div>";
            html += GetReportMeta(generatedBy, "RPT-INT-" + DateTime.Now.ToString("yyyyMMdd"));
            html += string.Format(@"
                <div class='stats-grid'>
                    <div class='stat-card-cell'>
                        <span class='stat-value' style='color:#9b59b6'>{0}</span>
                        <span class='stat-label'>Total Interviews</span>
                    </div>
                    <div class='stat-card-cell'>
                        <span class='stat-value' style='color:#9b59b6'>{1}</span>
                        <span class='stat-label'>Upcoming</span>
                    </div>
                </div>", interviews.Count, interviews.Count(i => i.ScheduledAt > DateTime.Now));

            html += @"<div style='padding: 0 40px 40px 40px;'>
                        <table class='report-table'>
                            <thead>
                                <tr style='background-color:#9b59b6'>
                                    <th>ID</th>
                                    <th>App ID</th>
                                    <th>Scheduled Date</th>
                                    <th>Mode</th>
                                    <th>Notes</th>
                                </tr>
                            </thead>
                            <tbody>";

            foreach (var i in interviews)
            {
                html += string.Format(@"
                <tr>
                    <td><strong>#{0}</strong></td>
                    <td>#{1}</td>
                    <td>{2:yyyy-MM-dd HH:mm}</td>
                    <td>{3}</td>
                    <td>{4}</td>
                </tr>", i.Id, i.ApplicationId, i.ScheduledAt, i.Mode, i.Notes);
            }

            html += "</tbody></table>" + GetPageFooter() + "</div></div>" + GetReportFooter();
            return html;
        }

        private void GenerateDepartmentCSV(List<Department> departments, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("ID,Name,Description,PositionCount");
                foreach (var dept in departments)
                {
                    writer.WriteLine(string.Format("{0},{1},{2},{3}", dept.Id, dept.Name, dept.Description, dept.Positions != null ? dept.Positions.Count : 0));
                }
            }
        }

        private string GenerateDepartmentPDF(string generatedBy)
        {
            var query = _uow.Departments.GetAll(d => d.Positions).AsQueryable();
            query = _tenantService.ApplyTenantFilter(query);
            var departments = query.ToList();
            var html = GetReportHeader("Departments Report", "Organizational structure and allocation", "#1abc9c", "#16a085");
            
            html += "<div class='report-frame'>";
            html += @"<div class='report-header-box' style='background: linear-gradient(135deg, #1abc9c 0%, #16a085 100%);'>
                        <h1>Departments Report</h1>
                        <p>Organizational structure and allocation</p>
                    </div>";
            html += GetReportMeta(generatedBy, "RPT-DEPT-" + DateTime.Now.ToString("yyyyMMdd"));

            html += @"<div style='padding: 0 40px 40px 40px;'>
                        <table class='report-table'>
                            <thead>
                                <tr style='background-color:#1abc9c'>
                                    <th>ID</th>
                                    <th>Department Name</th>
                                    <th>Description</th>
                                    <th>Open Positions</th>
                                </tr>
                            </thead>
                            <tbody>";

            foreach (var d in departments)
            {
                html += string.Format(@"
                <tr>
                    <td><strong>#{0}</strong></td>
                    <td>{1}</td>
                    <td>{2}</td>
                    <td>{3}</td>
                </tr>", d.Id, d.Name, d.Description, d.Positions != null ? d.Positions.Count : 0);
            }

            html += "</tbody></table>" + GetPageFooter() + "</div></div>" + GetReportFooter();
            return html;
        }

        private void GeneratePositionCSV(List<Position> positions, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("ID,Title,Department,SalaryMin,SalaryMax,Currency,Location,ApplicationsCount");
                foreach (var position in positions)
                {
                    writer.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7}", 
                        position.Id, position.Title, position.Department != null ? position.Department.Name : "", position.SalaryMin, position.SalaryMax, position.Currency, position.Location, position.Applications != null ? position.Applications.Count : 0));
                }
            }
        }

        private string GeneratePositionPDF(string generatedBy)
        {
            var query = _uow.Positions.GetAll(p => p.Department).AsQueryable();
            query = _tenantService.ApplyTenantFilter(query);
            var positions = query.ToList();
            var html = GetReportHeader("Positions Report", "Job vacancy listings and budget overview", "#2ecc71", "#27ae60");
            
            html += "<div class='report-frame'>";
            html += @"<div class='report-header-box' style='background: linear-gradient(135deg, #2ecc71 0%, #27ae60 100%);'>
                        <h1>Positions Report</h1>
                        <p>Job vacancy listings and budget overview</p>
                    </div>";
            html += GetReportMeta(generatedBy, "RPT-POS-" + DateTime.Now.ToString("yyyyMMdd"));

            html += @"<div style='padding: 0 40px 40px 40px;'>
                        <table class='report-table'>
                            <thead>
                                <tr style='background-color:#2ecc71'>
                                    <th>ID</th>
                                    <th>Job Title</th>
                                    <th>Department</th>
                                    <th>Salary Range</th>
                                    <th>Location</th>
                                </tr>
                            </thead>
                            <tbody>";

            foreach (var p in positions)
            {
                html += string.Format(@"
                <tr>
                    <td><strong>#{0}</strong></td>
                    <td>{1}</td>
                    <td>{2}</td>
                    <td>{3} {4:N0} - {5:N0}</td>
                    <td>{6}</td>
                </tr>", p.Id, p.Title, (p.Department != null ? p.Department.Name : "N/A"), (p.Currency ?? "KES"), p.SalaryMin, p.SalaryMax, p.Location);
            }

            html += "</tbody></table>" + GetPageFooter() + "</div></div>" + GetReportFooter();
            return html;
        }

        private void GenerateSecurityCSV(List<AuditLog> auditLogs, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("ID,Username,Action,Controller,Timestamp,IPAddress");
                foreach (var log in auditLogs)
                {
                    writer.WriteLine(string.Format("{0},{1},{2},{3},{4:yyyy-MM-dd HH:mm},{5}", log.Id, log.Username, log.Action, log.Controller, log.Timestamp, log.IPAddress));
                }
            }
        }

        private string GenerateSecurityPDF(string generatedBy)
        {
            var query = _uow.AuditLogs.GetAll().AsQueryable();
            query = _tenantService.ApplyTenantFilter(query);
            var auditLogs = query.OrderByDescending(l => l.Timestamp).Take(200).ToList();
            var html = GetReportHeader("Security & Audit Report", "Log of critical system actions and access", "#34495e", "#2c3e50");
            
            html += "<div class='report-frame'>";
            html += @"<div class='report-header-box' style='background: linear-gradient(135deg, #34495e 0%, #2c3e50 100%);'>
                        <h1>Security Audit Report</h1>
                        <p>Log of critical system actions and access</p>
                    </div>";
            html += GetReportMeta(generatedBy, "RPT-SEC-" + DateTime.Now.ToString("yyyyMMdd"));

            html += @"<div style='padding: 0 40px 40px 40px;'>
                        <table class='report-table'>
                            <thead>
                                <tr style='background-color:#34495e'>
                                    <th>Timestamp</th>
                                    <th>User</th>
                                    <th>Action</th>
                                    <th>Module</th>
                                    <th>IP Address</th>
                                </tr>
                            </thead>
                            <tbody>";

            foreach (var l in auditLogs)
            {
                html += string.Format(@"
                <tr>
                    <td>{0:yyyy-MM-dd HH:mm}</td>
                    <td><strong>{1}</strong></td>
                    <td>{2}</td>
                    <td>{3}</td>
                    <td>{4}</td>
                </tr>", l.Timestamp, l.Username, l.Action, l.Controller, l.IPAddress);
            }

            html += "</tbody></table>" + GetPageFooter() + "</div></div>" + GetReportFooter();
            return html;
        }

        public List<Report> GetActiveReports()
        {
            return _uow.Reports.GetAll().Where(r => r.IsActive).ToList();
        }

        public Report GetReport(int id)
        {
            return _uow.Reports.Get(id);
        }
    }
}
