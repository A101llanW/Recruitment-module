using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Security.Principal;
using System.Data.Entity;
using System.Linq;
using System.Web.Optimization;
using HR.Web.Data;
using HR.Web.Helpers;

namespace HR.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Ensure Razor view engine is registered
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new RazorViewEngine());

            // Disable automatic database changes to prevent schema conflicts
            Database.SetInitializer<HrContext>(null);

            // Ensure optional columns exist when migrations were not applied manually (EF still maps them).
            try
            {
                using (var db = new HrContext())
                {
                    DatabaseSchemaEnsure.ApplyOptionalColumns(db);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MvcApplication] DatabaseSchemaEnsure: " + ex.Message);
            }

            // Purge stale security records on startup (background — non-blocking)
            System.Threading.ThreadPool.QueueUserWorkItem(_ => PurgeStaleSecurityRecords());
        }

        private static void PurgeStaleSecurityRecords()
        {
            try
            {
                using (var db = new HrContext())
                {
                    var cutoffLogin = DateTime.Now.AddDays(-30);
                    var oldAttempts = db.LoginAttempts.Where(a => a.AttemptTime < cutoffLogin).ToList();
                    db.LoginAttempts.RemoveRange(oldAttempts);

                    var cutoffReset = DateTime.Now.AddDays(-7);
                    var oldResets = db.PasswordResets
                        .Where(r => (r.IsUsed || r.ExpiryDate < DateTime.UtcNow) && r.CreatedDate < cutoffReset)
                        .ToList();
                    db.PasswordResets.RemoveRange(oldResets);

                    db.SaveChanges();
                }
            }
            catch { /* Non-critical — purge failure is silent */ }
        }

        protected void Application_PostAuthenticateRequest(Object sender, EventArgs e)
        {
            if (Request == null)
            {
                return;
            }

            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
            {
                return;
            }

            if (IsPublicAuthPath(Request.Path))
            {
                return;
            }

            var ticket = FormsAuthentication.Decrypt(authCookie.Value);
            if (ticket == null || string.IsNullOrEmpty(ticket.UserData))
            {
                return;
            }

            if (!TryParseAuthTicketData(ticket, out var role, out var companyIdStr, out var accessToken, out var storedUaHash))
            {
                SetupPrincipal(ticket.Name, ticket.UserData);
                return;
            }

            if (!ValidateSessionAccessToken(ticket.Name, companyIdStr, accessToken))
            {
                InvalidateSession("session_invalid");
                return;
            }

            if (!ValidateUserAgentFingerprint(storedUaHash))
            {
                InvalidateSession("session_hijack");
                return;
            }

            SetAuthenticatedCompanyContext(companyIdStr);
            SetupPrincipal(ticket.Name, role);
        }

        private static bool IsPublicAuthPath(string requestPath)
        {
            if (string.IsNullOrEmpty(requestPath))
            {
                return false;
            }

            var path = requestPath.ToLower();
            return path.Contains("/account/login") ||
                path.Contains("/account/register") ||
                path.Contains("/account/confirmlegalconsent") ||
                path.Contains("/account/forgotpassword") ||
                path.Contains("/account/resetpassword") ||
                path.Contains("/account/verifymfa") ||
                path.Contains("/account/verifyemail") ||
                path.Contains("/account/setupmfa") ||
                path.Contains("/home/privacy") ||
                path.Contains("/home/terms") ||
                path.Contains("/home/error") ||
                path.Contains("/content/") ||
                path.Contains("/scripts/");
        }

        private static bool TryParseAuthTicketData(
            FormsAuthenticationTicket ticket,
            out string role,
            out string companyIdStr,
            out string accessToken,
            out string storedUaHash)
        {
            role = null;
            companyIdStr = null;
            accessToken = null;
            storedUaHash = null;

            if (ticket == null || string.IsNullOrEmpty(ticket.UserData))
            {
                return false;
            }

            var dataParts = ticket.UserData.Split('|');
            if (dataParts.Length < 3)
            {
                return false;
            }

            role = dataParts[0];
            companyIdStr = dataParts[1];
            accessToken = dataParts[2];
            storedUaHash = dataParts.Length > 3 ? dataParts[3] : null;
            return true;
        }

        private bool ValidateSessionAccessToken(string userName, string companyIdStr, string accessToken)
        {
            using (var db = new HrContext())
            {
                var query = db.Users.AsQueryable();
                if (!string.IsNullOrEmpty(companyIdStr) && int.TryParse(companyIdStr, out int cid))
                {
                    query = query.Where(u => u.UserName == userName && u.CompanyId == cid);
                }
                else
                {
                    query = query.Where(u => u.UserName == userName && u.CompanyId == null);
                }

                var user = query.FirstOrDefault();
                return user != null && user.AccessToken == accessToken;
            }
        }

        private bool ValidateUserAgentFingerprint(string storedUaHash)
        {
            if (string.IsNullOrEmpty(storedUaHash) || Request == null)
            {
                return true;
            }

            var currentUaHash = UserAgentFingerprint.Compute(Request.UserAgent);
            return currentUaHash == storedUaHash;
        }

        private static void SetAuthenticatedCompanyContext(string companyIdStr)
        {
            if (HttpContext.Current == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(companyIdStr) && int.TryParse(companyIdStr, out int authenticatedCompanyId))
            {
                HttpContext.Current.Items["AuthenticatedCompanyId"] = authenticatedCompanyId;
            }
        }

        private void InvalidateSession(string reason)
        {
            if (Response == null)
            {
                return;
            }

            FormsAuthentication.SignOut();
            var loginUrl = FormsAuthentication.LoginUrl;
            if (string.IsNullOrEmpty(loginUrl)) loginUrl = "~/Account/Login";
            Response.Redirect(string.Format("{0}?reason={1}", loginUrl, reason));
            Response.End();
        }

        private void SetupPrincipal(string username, string rolesString)
        {
            if (HttpContext.Current == null || string.IsNullOrEmpty(username))
            {
                return;
            }

            var identity = new GenericIdentity(username, "Forms");
            var rolesStringValue = rolesString ?? string.Empty;
            var roles = string.IsNullOrWhiteSpace(rolesStringValue)
                ? new string[] { }
                : rolesStringValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var principal = new GenericPrincipal(identity, roles);
            HttpContext.Current.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;
        }
    }
}
