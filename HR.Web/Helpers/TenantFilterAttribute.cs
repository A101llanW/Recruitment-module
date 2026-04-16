using System;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Services;

namespace HR.Web.Helpers
{
    public class TenantFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var tenantToken = filterContext.RouteData.Values["tenant"] as string;
            
            // 1. Resolve and set tenant context
            if (!string.IsNullOrEmpty(tenantToken))
            {
                using (var uow = new UnitOfWork())
                {
                    // Use direct context query to avoid loading all companies into memory
                    var company = uow.Context.Companies.FirstOrDefault(c => c.Slug == tenantToken && c.IsActive);
                    if (company != null)
                    {
                        // Store in HttpContext.Items for TenantService to pick up
                        filterContext.HttpContext.Items["TenantContext"] = company.Id;
                        
                        // Also make it available to views via ViewBag
                        filterContext.Controller.ViewBag.TenantContext = company;
                        filterContext.Controller.ViewBag.TenantToken = tenantToken;
                    }
                }
            }

            // 2. Automated Redirection for Authenticated Users
            var user = filterContext.HttpContext.User;
            if (user != null && user.Identity.IsAuthenticated)
            {
                var username = user.Identity.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    using (var uow = new UnitOfWork())
                    {
                        // Use direct context query to avoid loading all users into memory (Repository.cs:28)
                        string lowerUsername = username.ToLower();
                        int? companyId = null;
                        
                        var formsIdentity = user.Identity as System.Web.Security.FormsIdentity;
                        if (formsIdentity == null && user is System.Web.Security.RolePrincipal rolePrincipal)
                        {
                            formsIdentity = rolePrincipal.Identity as System.Web.Security.FormsIdentity;
                        }
                        
                        if (formsIdentity != null)
                        {
                            var props = formsIdentity.Ticket.UserData.Split('|');
                            if (props.Length >= 2 && int.TryParse(props[1], out int parsedId)) companyId = parsedId;
                        }

                        var dbUser = companyId.HasValue 
                            ? uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername && u.CompanyId == companyId.Value)
                            : uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername && u.CompanyId == null);
                            
                        if (dbUser == null)
                        {
                            dbUser = uow.Context.Users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
                        }
                        
                        if (dbUser != null)
                        {
                            // 3. Email Verification Enforcement
                            if (!dbUser.IsEmailVerified)
                            {
                                var currentController = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
                                var currentAction = filterContext.ActionDescriptor.ActionName;

                                // Allow verification actions and logout to bypass the redirect
                                var isVerificationAction = currentController == "Account" && 
                                    (currentAction == "VerifyEmail" || currentAction == "VerifyEmailSubmit" || 
                                     currentAction == "SendVerificationEmail" || currentAction == "UpdateAndSendVerification" || currentAction == "Logout");

                                if (!isVerificationAction)
                                {
                                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary {
                                        { "controller", "Account" },
                                        { "action", "VerifyEmail" },
                                        { "tenant", tenantToken }
                                    });
                                    return;
                                }
                            }

                            if (dbUser.CompanyId.HasValue)
                            {
                                var userCompany = uow.Context.Companies.FirstOrDefault(c => c.Id == dbUser.CompanyId.Value);
                                if (userCompany != null && userCompany.IsActive)
                                {
                                    // If the current URL is global OR it's the wrong company URL, redirect to the correct one
                                    if (string.IsNullOrEmpty(tenantToken) || !string.Equals(tenantToken, userCompany.Slug, StringComparison.OrdinalIgnoreCase))
                                    {
                                        var routeValues = new System.Web.Routing.RouteValueDictionary(filterContext.RouteData.Values);
                                        routeValues["tenant"] = userCompany.Slug;
                                        
                                        // Preserve query string parameters
                                        foreach (string key in filterContext.HttpContext.Request.QueryString.AllKeys)
                                        {
                                            if (key != null) routeValues[key] = filterContext.HttpContext.Request.QueryString[key];
                                        }

                                        filterContext.Result = new RedirectToRouteResult("Tenant", routeValues);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
