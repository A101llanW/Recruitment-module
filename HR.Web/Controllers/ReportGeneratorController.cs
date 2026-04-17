using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "Admin, SuperAdmin")]
    [RoleBasedAuthorization("Admin")]
    public class ReportGeneratorController : Controller
    {
        private readonly ReportService _reportService = new ReportService();

        // GET: ReportGenerator
        public ActionResult Index()
        {
            return View("~/Views/Reports/Index.cshtml");
        }

        // POST: ReportGenerator/GenerateDirect
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateDirect(string reportType, string format = "csv")
        {
            try
            {
                if (string.IsNullOrEmpty(reportType))
                {
                    return Json(new { success = false, message = "Please select a report type" });
                }

                var filePath = _reportService.GenerateReportByType(reportType, User.Identity.Name, format);
                var fileName = Path.GetFileName(filePath);
                
                return Json(new { 
                    success = true, 
                    message = string.Format("Report '{0}' generated successfully", fileName),
                    fileName = fileName,
                    filePath = filePath
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error generating report: " + ex.Message });
            }
        }

        // POST: ReportGenerator/Preview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Preview(string reportType)
        {
            try
            {
                if (string.IsNullOrEmpty(reportType))
                {
                    return Json(new { success = false, message = "Please select a report type" });
                }

                var html = _reportService.PreviewReportByType(reportType, User.Identity.Name);
                
                return Json(new { 
                    success = true, 
                    html = html
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error generating preview: " + ex.Message });
            }
        }

        // GET: ReportGenerator/Download?fileName=...
        public ActionResult Download(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return HttpNotFound();
                }

                var filePath = Path.Combine(Server.MapPath("~/Reports"), fileName);
                if (!System.IO.File.Exists(filePath))
                {
                    return HttpNotFound();
                }

                string extension = Path.GetExtension(fileName).ToLower();
                string contentType = "application/octet-stream";
                
                if (extension == ".csv") contentType = "text/csv";
                else if (extension == ".pdf") contentType = "application/pdf";
                else if (extension == ".html") contentType = "text/html";

                return File(filePath, contentType, fileName);
            }
            catch
            {
                return HttpNotFound();
            }
        }
    }
}
