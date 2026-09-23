using System.Web.Mvc;
using System.Net;
using HR.Web.Helpers;

namespace HR.Web.Controllers
{
    [AllowAnonymous]
    public class ErrorController : Controller
    {
        // ── Generic Error ─────────────────────────────────────────────
        public ActionResult Index()
        {
            SafeNotFoundHandler.ApplyBrandedErrorStatus(HttpContext, (int)HttpStatusCode.InternalServerError);
            return View();
        }

        // ── 404 Not Found ─────────────────────────────────────────────
        public ActionResult NotFound()
        {
            SafeNotFoundHandler.ApplyBrandedErrorStatus(HttpContext, (int)HttpStatusCode.NotFound);
            return View();
        }

        // ── 403 Forbidden ─────────────────────────────────────────────
        public ActionResult Forbidden()
        {
            SafeNotFoundHandler.ApplyBrandedErrorStatus(HttpContext, (int)HttpStatusCode.Forbidden);
            return View();
        }
    }
}
