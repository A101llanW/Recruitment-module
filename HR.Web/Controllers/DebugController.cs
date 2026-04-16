using System;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Controllers
{
    /// <summary>
    /// Internal diagnostic console — restricted to SuperAdmin role AND localhost requests only.
    /// All dangerous recovery actions (UnlockSuperAdmin, GenerateSuperAdminResetLink) have been
    /// permanently removed. Use the standard ForgotPassword flow or direct DB access for recovery.
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    public class DebugController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();

        /// <summary>
        /// Guards every action — returns 404 if the request does not originate from localhost.
        /// This prevents the controller from being discoverable or accessible from the network,
        /// even if the Authorize attribute is somehow bypassed.
        /// </summary>
        private bool IsLocalRequest()
        {
            var userHostAddress = Request.UserHostAddress;
            return userHostAddress == "127.0.0.1"
                || userHostAddress == "::1"
                || userHostAddress == "localhost";
        }

        // ── System Console ──────────────────────────────────────────────────────
        public ActionResult SystemConsole()
        {
            if (!IsLocalRequest()) return HttpNotFound();

            var logs = _uow.AuditLogs.GetAll()
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToList();

            var html = string.Format(@"
                <style>
                    body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #1e1e1e; color: #d4d4d4; padding: 20px; }}
                    .section {{ background: #252526; border-radius: 8px; padding: 20px; margin-bottom: 20px; border: 1px solid #3e3e42; }}
                    h2 {{ color: #569cd6; margin-top: 0; border-bottom: 1px solid #3e3e42; padding-bottom: 10px; }}
                    .warning {{ background: #3e2a00; border: 1px solid #f0a500; color: #f0a500; padding: 12px 16px; border-radius: 6px; margin-bottom: 20px; }}
                    table {{ width: 100%; border-collapse: collapse; font-size: 0.9rem; }}
                    th {{ text-align: left; color: #ce9178; border-bottom: 2px solid #3e3e42; padding: 10px; }}
                    td {{ padding: 10px; border-bottom: 1px solid #3e3e42; }}
                    .timestamp {{ color: #6a9955; font-family: 'Consolas', monospace; }}
                    .action {{ color: #dcdcaa; font-weight: bold; }}
                </style>
                <div class='warning'>
                    ⚠️ <strong>LOCALHOST ACCESS ONLY</strong> — This console is not accessible from the network.
                    It is visible here because you are on the server itself.
                </div>
                <h2>System Console</h2>
                <div class='section'>
                    <h2>Recent Audit Events (last 100)</h2>
                    <table>
                        <thead>
                            <tr><th>Time</th><th>User</th><th>Action</th><th>Detail</th></tr>
                        </thead>
                        <tbody>");

            foreach (var log in logs)
            {
                html += string.Format(@"
                        <tr>
                            <td class='timestamp'>{0:yyyy-MM-dd HH:mm:ss}</td>
                            <td>{1}</td>
                            <td class='action'>{2}</td>
                            <td>{3}</td>
                        </tr>",
                    log.Timestamp,
                    System.Web.HttpUtility.HtmlEncode(log.Username ?? ""),
                    System.Web.HttpUtility.HtmlEncode(log.Action ?? ""),
                    System.Web.HttpUtility.HtmlEncode(log.ErrorMessage ?? ""));
            }

            html += "</tbody></table></div>";
            return Content(html, "text/html");
        }

        // ── Health Check ────────────────────────────────────────────────────────
        public ActionResult Index()
        {
            if (!IsLocalRequest()) return HttpNotFound();
            return Content(string.Format(
                "Debug Console Active | Server: {0} | Time: {1:yyyy-MM-dd HH:mm:ss} UTC",
                Environment.MachineName, DateTime.UtcNow));
        }
    }
}
