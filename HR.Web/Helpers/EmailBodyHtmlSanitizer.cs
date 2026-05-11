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
            s = Regex.Replace(s, @"\s+on\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+href\s*=\s*([""'])javascript:[^""']*\1", " href=\"#\"", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+src\s*=\s*([""'])javascript:[^""']*\1", " src=\"\"", RegexOptions.IgnoreCase);

            return s;
        }
    }
}
