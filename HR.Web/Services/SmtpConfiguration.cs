namespace HR.Web.Services
{
    /// <summary>
    /// Resolved SMTP connection and From header values for a single send operation.
    /// </summary>
    public sealed class SmtpConfiguration
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public bool EnableSsl { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
        public bool IsCompanyScoped { get; set; }

        public bool IsUsable()
        {
            return !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromEmail);
        }
    }
}
