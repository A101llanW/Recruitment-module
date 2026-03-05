using System.Web;
using System.Web.Optimization;

namespace HR.Web
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            // CSS Bundles
            var cssBundle = new StyleBundle("~/Content/css");
            cssBundle.Include("~/Content/css/bootstrap.min.css", "~/Content/css/font-awesome.min.css");
            bundles.Add(cssBundle);

            // Script Bundles
            var scriptBundle = new ScriptBundle("~/Scripts/js");
            scriptBundle.Include("~/Scripts/jquery-3.6.0.min.js", "~/Scripts/bootstrap.bundle.min.js");
            bundles.Add(scriptBundle);

            var validationBundle = new ScriptBundle("~/Scripts/validation");
            validationBundle.Include("~/Scripts/jquery.validate.min.js", "~/Scripts/jquery.validate.unobtrusive.min.js");
            bundles.Add(validationBundle);

            // Admin scripts bundle
            var adminBundle = new ScriptBundle("~/Scripts/admin");
            adminBundle.Include("~/Scripts/Sortable.min.js");
            bundles.Add(adminBundle);
        }
    }
}










































