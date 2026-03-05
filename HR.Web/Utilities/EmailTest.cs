using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace HR.Web.Utilities
{
    public class EmailTest
    {
        public static async Task<bool> TestEmailConfiguration()
        {
            try
            {
                // Read configuration
                var smtpHost = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
                var smtpUser = ConfigurationManager.AppSettings["SmtpUser"] ?? "";
                var smtpPass = ConfigurationManager.AppSettings["SmtpPassword"] ?? "";
                var enableSsl = bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"] ?? "true");
                var fromEmail = ConfigurationManager.AppSettings["FromEmail"] ?? "noreply@nanosoft.com";
                var fromName = ConfigurationManager.AppSettings["FromName"] ?? "Nanosoft HR System";

                Console.WriteLine("Email Configuration Test");
                Console.WriteLine("========================");
                Console.WriteLine($"SMTP Host: {smtpHost}");
                Console.WriteLine($"SMTP Port: {smtpPort}");
                Console.WriteLine($"SSL Enabled: {enableSsl}");
                Console.WriteLine($"From Email: {fromEmail}");
                Console.WriteLine($"From Name: {fromName}");
                Console.WriteLine($"SMTP User: {smtpUser}");
                Console.WriteLine($"SMTP Password: {(string.IsNullOrEmpty(smtpPass) ? "NOT SET" : "SET")}");
                Console.WriteLine();

                // Test SMTP connection
                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = enableSsl;
                    client.UseDefaultCredentials = false;
                    
                    if (!string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPass))
                    {
                        client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                    }

                    // Test connection by sending a test email
                    var testEmail = smtpUser; // Send to self for testing
                    var subject = "HR System Email Test";
                    var body = $@"
<!DOCTYPE html>
<html>
<body>
    <h2>Email Configuration Test</h2>
    <p>This is a test email from the Nanosoft HR System to verify email configuration.</p>
    <p><strong>Sent:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    <p><strong>SMTP Server:</strong> {smtpHost}:{smtpPort}</p>
    <p><strong>SSL:</strong> {enableSsl}</p>
    <hr>
    <p><em>If you receive this email, the email configuration is working correctly.</em></p>
</body>
</html>";

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(fromEmail, fromName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(testEmail);

                    Console.WriteLine("Sending test email...");
                    await client.SendMailAsync(mailMessage);
                    
                    Console.WriteLine($"✅ Test email sent successfully to: {testEmail}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Email test failed: {ex.Message}");
                Console.WriteLine($"Full error: {ex}");
                return false;
            }
        }

        public static void ShowEmailStatus()
        {
            Console.WriteLine("Email System Status");
            Console.WriteLine("==================");
            
            var smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
            var smtpUser = ConfigurationManager.AppSettings["SmtpUser"];
            var smtpPass = ConfigurationManager.AppSettings["SmtpPassword"];
            
            Console.WriteLine($"SMTP Host: {smtpHost ?? "NOT CONFIGURED"}");
            Console.WriteLine($"SMTP User: {smtpUser ?? "NOT CONFIGURED"}");
            Console.WriteLine($"SMTP Password: {(string.IsNullOrEmpty(smtpPass) ? "NOT CONFIGURED" : "CONFIGURED")}");
            
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
            {
                Console.WriteLine("\n❌ Email system is NOT properly configured");
                Console.WriteLine("Please configure SMTP settings in secrets.config");
            }
            else
            {
                Console.WriteLine("\n✅ Email system appears to be configured");
                Console.WriteLine("Features that use email:");
                Console.WriteLine("  • Forgot Password");
                Console.WriteLine("  • MFA Code Delivery");
                Console.WriteLine("  • User Notifications");
            }
            
            Console.WriteLine($"\n📝 MFA codes are also logged to: mfa_codes.txt");
        }
    }
}
