using System;
using System.Web;
using System.Web.Mvc;

namespace HR.Web.Filters
{
    public class NoCacheAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var response = filterContext.HttpContext.Response;

            // Set headers to prevent caching of sensitive data
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Cache.SetNoStore();
            response.Cache.SetExpires(DateTime.UtcNow.AddHours(-1));
            response.Cache.AppendCacheExtension("no-cache");
            response.Cache.SetProxyMaxAge(new TimeSpan(0));
            
            // Standard HTTP 1.0/1.1 headers
            response.AddHeader("Pragma", "no-cache");
            response.AddHeader("Expires", "0");

            base.OnResultExecuting(filterContext);
        }
    }
}
