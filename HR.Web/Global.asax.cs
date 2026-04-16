using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Security.Principal;
using System.Data.Entity;
using System.Linq;
using System.Web.Optimization;
using HR.Web.Data;

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

            // Purge stale security records on startup (background — non-blocking)
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (var db = new HrContext())
                    {
                        // Purge login attempts older than 30 days
                        var cutoffLogin = DateTime.Now.AddDays(-30);
                        var oldAttempts = db.LoginAttempts.Where(a => a.AttemptTime < cutoffLogin).ToList();
                        db.LoginAttempts.RemoveRange(oldAttempts);

                        // Purge used or expired password reset tokens older than 7 days
                        var cutoffReset = DateTime.Now.AddDays(-7);
                        var oldResets = db.PasswordResets
                            .Where(r => (r.IsUsed || r.ExpiryDate < DateTime.UtcNow) && r.CreatedDate < cutoffReset)
                            .ToList();
                        db.PasswordResets.RemoveRange(oldResets);

                        db.SaveChanges();
                    }
                }
                catch { /* Non-critical — purge failure is silent */ }
            });
        }

        protected void Application_PostAuthenticateRequest(Object sender, EventArgs e)
        {
            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
            {
                return;
            }

            // Skip session validation for login/public/verification pages to prevent redirect loops with stale cookies
            var path = Request.Path.ToLower();
            if (path.Contains("/account/login") || 
                path.Contains("/account/register") || 
                path.Contains("/account/forgotpassword") ||
                path.Contains("/account/resetpassword") ||
                path.Contains("/account/verifymfa") ||
                path.Contains("/account/verifyemail") ||
                path.Contains("/account/setupmfa") ||
                path.Contains("/home/error") ||
                path.Contains("/content/") ||
                path.Contains("/scripts/"))
            {
                return;
            }

            var ticket = FormsAuthentication.Decrypt(authCookie.Value);
            if (ticket == null || string.IsNullOrEmpty(ticket.UserData))
            {
                return;
            }

            // UserData Structure: Role|CompanyId|AccessToken|UAHash
            var dataParts = ticket.UserData.Split('|');
            if (dataParts.Length < 3)
            {
                // Fallback for old tickets without tokens
                SetupPrincipal(ticket.Name, ticket.UserData);
                return;
            }

            string role         = dataParts[0];
            string companyIdStr = dataParts[1];
            string accessToken  = dataParts[2];
            string storedUaHash = dataParts.Length > 3 ? dataParts[3] : null;

            // --- LAYER 1: Session Token Validation (catches admin-forced logouts & deleted accounts) ---
            using (var db = new HrContext())
            {
                // Disambiguate user using Username AND CompanyId (prevents collisions between users with same name in different companies)
                var query = db.Users.AsQueryable();
                
                if (!string.IsNullOrEmpty(companyIdStr) && int.TryParse(companyIdStr, out int cid))
                {
                    query = query.Where(u => u.UserName == ticket.Name && u.CompanyId == cid);
                }
                else
                {
                    query = query.Where(u => u.UserName == ticket.Name && u.CompanyId == null);
                }

                var user = query.FirstOrDefault();
                
                if (user == null || user.AccessToken != accessToken)
                {
                    InvalidateSession("session_invalid");
                    return;
                }
            }

            // --- LAYER 2: Browser Fingerprint Validation (catches stolen cookie replay from different browser/tool) ---
            if (!string.IsNullOrEmpty(storedUaHash))
            {
                var currentUaHash = GetFingerprint(Request.UserAgent);
                if (currentUaHash != storedUaHash)
                {
                    InvalidateSession("session_hijack");
                    return;
                }
            }

            if (!string.IsNullOrEmpty(companyIdStr) && int.TryParse(companyIdStr, out int authenticatedCompanyId))
            {
                HttpContext.Current.Items["AuthenticatedCompanyId"] = authenticatedCompanyId;
            }

            SetupPrincipal(ticket.Name, role);
        }

        private void InvalidateSession(string reason)
        {
            FormsAuthentication.SignOut();
            var loginUrl = FormsAuthentication.LoginUrl;
            if (string.IsNullOrEmpty(loginUrl)) loginUrl = "~/Account/Login";
            Response.Redirect(string.Format("{0}?reason={1}", loginUrl, reason));
            Response.End();
        }

        /// <summary>
        /// Computes a short, non-reversible SHA256 fingerprint of the browser's User-Agent string.
        /// This helps detect if a stolen cookie is being replayed from a different browser or automated tool.
        /// </summary>
        private string GetFingerprint(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "unknown";
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(userAgent));
                return Convert.ToBase64String(hash).Substring(0, 16); // 16 chars is sufficient
            }
        }

        private void SetupPrincipal(string username, string rolesString)
        {
            var identity = new GenericIdentity(username, "Forms");
            var roles = string.IsNullOrWhiteSpace(rolesString)
                ? new string[] { }
                : rolesString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var principal = new GenericPrincipal(identity, roles);
            HttpContext.Current.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;
        }
    }
}
