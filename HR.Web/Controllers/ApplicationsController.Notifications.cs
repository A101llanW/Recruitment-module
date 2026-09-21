using System.Web.Mvc;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    public partial class ApplicationsController
    {
        private readonly ApplicationNotificationService _applicationNotificationService = new ApplicationNotificationService();

        [AllowAnonymous]
        public ActionResult NotificationSummary(string token)
        {
            ViewBag.SuppressPageHero = true;
            ViewBag.HideNavbar = true;
            var summary = _applicationNotificationService.BuildSummaryForToken(token);
            return View(summary);
        }

        private void NotifyCompanyOfNewApplication(int applicationId)
        {
            _applicationNotificationService.QueueNewApplicationNotifications(applicationId, Request);
        }
    }
}
