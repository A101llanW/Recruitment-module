using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace HR.Web.Filters
{
    public class FreshPortalSecurityAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Check if this is a fresh portal session
            if (filterContext.HttpContext.Session != null)
            {
                var isFreshPortalSession = filterContext.HttpContext.Session["IsFreshPortalSession"] as bool?;
                var originalUserAgent = filterContext.HttpContext.Session["OriginalUserAgent"] as string;
                
                if (isFreshPortalSession == true)
                {
                    // This is a fresh portal session - restrict access to only admin-level functions
                    var currentUrl = filterContext.HttpContext.Request.Url.ToString();
                    
                    // List of RESTRICTED pages for fresh portal sessions (admin functions only)
                    var restrictedPages = new[]
                    {
                        "/Admin/",
                        "/Companies/",
                        "/Licenses/",
                        "/Reports/",
                        "/ReportGenerator/"
                    };
                    
                    foreach (var restrictedPage in restrictedPages)
                    {
                        if (currentUrl.ToLower().Contains(restrictedPage.ToLower()))
                        {
                            // Block access and show error
                            filterContext.Result = new ViewResult
                            {
                                ViewName = "~/Views/Shared/FreshPortalRestriction.cshtml"
                            };
                            return;
                        }
                    }
                    
                    // Allow normal user activities (login, register, applications, etc.)
                    // Only block if somehow a SuperAdmin gets authenticated in fresh session
                    if (filterContext.HttpContext.User != null && 
                        filterContext.HttpContext.User.Identity != null && 
                        filterContext.HttpContext.User.Identity.IsAuthenticated)
                    {
                        // Check if this is a SuperAdmin who somehow got authenticated in fresh session
                        var username = filterContext.HttpContext.User.Identity.Name;
                        var uow = new Data.UnitOfWork();
                        var lowerUsername = username.ToLower();
                        var user = uow.Users.GetAll().FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
                        
                        if (user != null && !user.CompanyId.HasValue && user.Role == "Admin")
                        {
                            // SuperAdmin in fresh session - log them out immediately
                            FormsAuthentication.SignOut();
                            filterContext.HttpContext.Session.Abandon();
                            
                            filterContext.Result = new ViewResult
                            {
                                ViewName = "~/Views/Shared/FreshPortalRestriction.cshtml"
                            };
                            return;
                        }
                    }
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
