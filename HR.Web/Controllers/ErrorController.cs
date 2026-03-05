using System.Web.Mvc;
using System.Net;

namespace HR.Web.Controllers
{
    public class ErrorController : Controller
    {
        // ── Generic Error ─────────────────────────────────────────────
        public ActionResult Index()
        {
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return View();
        }

        // ── 404 Not Found ─────────────────────────────────────────────
        public ActionResult NotFound()
        {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return View();
        }

        // ── 403 Forbidden ─────────────────────────────────────────────
        public ActionResult Forbidden()
        {
            Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return View();
        }
    }
}
