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
    [ModuleAccess(RoleModuleCatalog.Reports)]
    public class ReportsController : Controller
    {
        // GET: Reports
        public ActionResult Index()
        {
            return View();
        }

        // GET: Reports/Create (redirect to Index to break redirect loop)
        public ActionResult Create()
        {
            return RedirectToAction("Index");
        }

        // POST: Reports/GenerateDirect
        [HttpPost]
        public ActionResult GenerateDirect(string reportType)
        {
            try
            {
                if (string.IsNullOrEmpty(reportType))
                {
                    return Json(new { success = false, message = "Please select a report type" });
                }

                // Test response without ReportService
                return Json(new { 
                    success = true, 
                    message = string.Format("Test report for {0}", reportType),
                    fileName = string.Format("test_{0}.csv", reportType),
                    filePath = "/Reports/test.csv"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // GET: Reports/Download?fileName=...
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

                return File(filePath, "text/csv", fileName);
            }
            catch
            {
                return HttpNotFound();
            }
        }
    }
}
