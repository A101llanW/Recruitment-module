using System.Text.RegularExpressions;

namespace HR.Web.Helpers
{
    /// <summary>
    /// Removes common XSS vectors from admin-composed HTML email bodies.
    /// </summary>
    public static class EmailBodyHtmlSanitizer
    {
        public static string Sanitize(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var s = html.Trim();

            s = Regex.Replace(s, @"<script[^>]*>[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</?script[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<iframe[\s\S]*?</iframe>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<object[\s\S]*?</object>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<embed[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<form[\s\S]*?</form>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<meta[^>]+http-equiv\s*=\s*([""']?)refresh\1[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<link[^>]+href\s*=\s*([""']?)javascript:[^""']*\1[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+on\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+href\s*=\s*([""'])javascript:[^""']*\1", " href=\"#\"", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+src\s*=\s*([""'])javascript:[^""']*\1", " src=\"\"", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+href\s*=\s*([""'])vbscript:[^""']*\1", " href=\"#\"", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+src\s*=\s*([""'])vbscript:[^""']*\1", " src=\"\"", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+href\s*=\s*([""'])data:text/html[^""']*\1", " href=\"#\"", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"javascript\s*:", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"vbscript\s*:", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"expression\s*\(", string.Empty, RegexOptions.IgnoreCase);

            return s;
        }
    }
}
