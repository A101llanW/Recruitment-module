using System;
using System.Diagnostics;
using System.Web;

namespace HR.Web.Helpers
{
    /// <summary>
    /// Writes one-time auth codes to the Visual Studio Output window during local debugging.
    /// </summary>
    public static class DevDiagnostics
    {
        public static void LogOneTimeCode(string category, string email, string code)
        {
            if (!IsEnabled())
            {
                return;
            }

            var block = string.Format(
                "=== {0} ==={1}Email: {2}{1}Code: {3}{1}========================",
                category,
                Environment.NewLine,
                string.IsNullOrWhiteSpace(email) ? "(none)" : email.Trim(),
                string.IsNullOrWhiteSpace(code) ? "(none)" : code);

            Debug.WriteLine(block);
            Trace.WriteLine(block);
        }

        public static bool IsEnabled()
        {
#if DEBUG
            return true;
#else
            try
            {
                return HttpContext.Current != null && HttpContext.Current.IsDebuggingEnabled;
            }
            catch
            {
                return false;
            }
#endif
        }
    }
}
