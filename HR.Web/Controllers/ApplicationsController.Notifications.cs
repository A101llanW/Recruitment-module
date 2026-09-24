using System;
using System.Web.Mvc;
using HR.Web.Helpers;
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

            try
            {
                var summary = _applicationNotificationService.BuildSummaryForToken(token);
                return View(summary);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private void NotifyCompanyOfNewApplication(int applicationId)
        {
            var smtpSettings = new CompanySmtpSettingsService(_uow.Context, new SettingsService());
            var emailService = new EmailService(smtpSettings);
            var notificationService = new ApplicationNotificationService(_uow.Context, emailService, new SecurityService());
            notificationService.QueueNewApplicationNotifications(applicationId, Request);
        }
    }
}
