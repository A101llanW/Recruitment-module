using System;
using System.IO;
using System.Web.Mvc;

namespace HR.Web.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return HttpNotFound();
        }

        public ActionResult Debug()
        {
            return View();
        }

        public ActionResult About()
        {
            return View();
        }

        /// <summary>
        /// Serve company logo image
        /// </summary>
        [AllowAnonymous]
        public ActionResult CompanyLogo()
        {
            try
            {
                // Try the new transparent logo first
                string logoPath = Server.MapPath("~/Content/images/nanosoft-logo-transparent.png");
                if (System.IO.File.Exists(logoPath))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(logoPath);
                    return File(fileBytes, "image/png");
                }
                
                // Fallback to the JPG version
                logoPath = Server.MapPath("~/Content/images/nanosoft-logo.jpg");
                if (System.IO.File.Exists(logoPath))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(logoPath);
                    return File(fileBytes, "image/jpeg");
                }
                
                // Fallback to the original logo
                logoPath = Server.MapPath("~/Content/images/company-logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(logoPath);
                    return File(fileBytes, "image/png");
                }
                else
                {
                    // Return a simple placeholder or 404
                    return HttpNotFound("Logo file not found");
                }
            }
            catch (Exception ex)
            {
                // Log error if needed
                return HttpNotFound("Error loading logo");
            }
        }
        // ── Error Pages (referenced by customErrors in Web.config) ──────────
        [AllowAnonymous]
        public ActionResult Error()
        {
            Response.StatusCode = 500;
            return View("~/Views/Error/Index.cshtml");
        }

        [AllowAnonymous]
        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            return View("~/Views/Error/NotFound.cshtml");
        }

        [AllowAnonymous]
        public ActionResult Forbidden()
        {
            Response.StatusCode = 403;
            return View("~/Views/Error/Forbidden.cshtml");
        }
    }
}
