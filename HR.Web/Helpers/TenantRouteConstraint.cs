using System;
using System.Linq;
using System.Web;
using System.Web.Routing;
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
                    // Validate token against database using Slug
                    // Note: We use a fresh UnitOfWork here to avoid context issues in the routing engine
                    using (var uow = new UnitOfWork())
                    {
                        var companies = uow.Companies.GetAll().ToList();
                        System.Diagnostics.Debug.WriteLine(string.Format("TenantRouteConstraint: Found {0} companies in database", companies.Count));
                        
                        var matchingCompany = companies.FirstOrDefault(c => 
                            c.Slug != null && 
                            c.Slug.Equals(token, StringComparison.OrdinalIgnoreCase) && 
                            c.IsActive);
                        bool isMatch = matchingCompany != null;
                        
                        System.Diagnostics.Debug.WriteLine(string.Format("TenantRouteConstraint: Token '{0}' match = {1}", token, isMatch));
                        
                        return isMatch;
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
}
