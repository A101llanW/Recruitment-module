using System;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Data.Entity;
using HR.Web.Controllers;
using HR.Web.Data;

namespace HR.Web.Helpers
{
    public class TenantRouteConstraint : IRouteConstraint
    {
        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (values.ContainsKey(parameterName))
            {
                var token = values[parameterName] as string;
                if (string.IsNullOrWhiteSpace(token)) 
                {
                    // Debug: Log empty token
                    System.Diagnostics.Debug.WriteLine(string.Format("TenantRouteConstraint: Empty token for parameter {0}", parameterName));
                    return false;
                }

                // Debug: Log the token being checked
                System.Diagnostics.Debug.WriteLine(string.Format("TenantRouteConstraint: Checking token '{0}'", token));

                try
                {
                    using (var uow = new UnitOfWork())
                    {
                        var matchingCompany = uow.Context.Companies
                            .AsNoTracking()
                            .FirstOrDefault(c => c.Slug == token && c.IsActive);
                        return matchingCompany != null;
                    }
                }
                catch (Exception ex)
                {
                    // Debug: Log any database errors
                    System.Diagnostics.Debug.WriteLine(string.Format("TenantRouteConstraint: Database error - {0}", ex.Message));
                    return false;
                }
            }
            
            // Debug: Log missing parameter
            System.Diagnostics.Debug.WriteLine(string.Format("TenantRouteConstraint: Parameter '{0}' not found in route values", parameterName));
            return false;
        }
    }

    /// <summary>
    /// Routes unresolved controller names (e.g. /Candidates, /Questionnaires)
    /// to the branded Error/NotFound page instead of the default ASP.NET 404.
    /// </summary>
    public class SafeControllerFactory : DefaultControllerFactory
    {
        protected override Type GetControllerType(RequestContext requestContext, string controllerName)
        {
            var controllerType = base.GetControllerType(requestContext, controllerName);
            if (controllerType != null)
            {
                return controllerType;
            }

            requestContext.RouteData.Values["controller"] = "Error";
            requestContext.RouteData.Values["action"] = "NotFound";
            return typeof(ErrorController);
        }
    }

    /// <summary>
    /// Renders the branded 404 page for unknown routes/controllers/actions
    /// without leaking ASP.NET framework version details.
    /// </summary>
    public static class SafeNotFoundHandler
    {
        public static bool IsNotFoundException(Exception exception)
        {
            var current = exception;
            while (current != null)
            {
                var httpException = current as HttpException;
                if (httpException != null && httpException.GetHttpCode() == (int)HttpStatusCode.NotFound)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        public static bool ShouldHandleRequest(HttpContext context)
        {
            if (context == null || context.Request == null)
            {
                return false;
            }

            var path = (context.Request.Path ?? string.Empty).ToLowerInvariant();
            if (path.Contains("/content/") ||
                path.Contains("/scripts/") ||
                path.EndsWith(".axd", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public static void ApplyBrandedErrorStatus(HttpContextBase context, int statusCode)
        {
            if (context == null || context.Response == null)
            {
                return;
            }

            context.Response.TrySkipIisCustomErrors = true;
            // When customErrors is already serving this request as the configured error
            // page, setting the status code again retriggers customErrors → redirect loop.
            if (!context.IsCustomErrorEnabled)
            {
                context.Response.StatusCode = statusCode;
            }
        }

        public static void ApplyBrandedErrorStatus(HttpResponse response, int statusCode)
        {
            if (response == null)
            {
                return;
            }

            response.TrySkipIisCustomErrors = true;
            var context = HttpContext.Current;
            if (context == null || !context.IsCustomErrorEnabled)
            {
                response.StatusCode = statusCode;
            }
        }

        public static void ExecuteBrandedNotFound(HttpContext context)
        {
            if (context == null || !ShouldHandleRequest(context))
            {
                return;
            }

            context.Server.ClearError();
            context.Response.Clear();
            ApplyBrandedErrorStatus(context.Response, (int)HttpStatusCode.NotFound);

            var routeData = new RouteData();
            routeData.Values["controller"] = "Error";
            routeData.Values["action"] = "NotFound";

            var requestContext = new RequestContext(new HttpContextWrapper(context), routeData);
            IController controller = new ErrorController();
            controller.Execute(requestContext);
            context.ApplicationInstance.CompleteRequest();
        }

        public static void ReplaceWithBrandedNotFound(ResultExecutingContext filterContext)
        {
            if (filterContext == null || filterContext.Result == null)
            {
                return;
            }

            var statusCodeResult = filterContext.Result as HttpStatusCodeResult;
            if (statusCodeResult == null || statusCodeResult.StatusCode != (int)HttpStatusCode.NotFound)
            {
                return;
            }

            ApplyBrandedErrorStatus(filterContext.HttpContext, (int)HttpStatusCode.NotFound);
            filterContext.Result = new ViewResult
            {
                ViewName = "~/Views/Error/NotFound.cshtml",
                ViewData = filterContext.Controller != null ? filterContext.Controller.ViewData : new ViewDataDictionary(),
                TempData = filterContext.Controller != null ? filterContext.Controller.TempData : new TempDataDictionary()
            };
        }
    }
}
