using System;
using System.Net;
using System.Net.Mail;

namespace HR.Web.Utilities
{
    public class SmtpTest
    {
        public static void TestSmtpConnection()
        {
            try
            {
                var smtpHost = "smtp.gmail.com";
                var smtpPort = 587;
                var smtpUser = "nanosoft.africa@gmail.com";
                var smtpPass = "rhhbtungmedmdksr";
                var enableSsl = true;

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = enableSsl;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                    client.Timeout = 10000; // 10 seconds

                    Console.WriteLine("Testing SMTP connection...");
                    Console.WriteLine($"Host: {smtpHost}:{smtpPort}");
                    Console.WriteLine($"User: {smtpUser}");
                    Console.WriteLine($"SSL: {enableSsl}");
                    Console.WriteLine($"Password length: {smtpPass.Length}");

                    // Test with a simple message
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress("nanosoft.africa@gmail.com", "Test"),
                        Subject = "SMTP Test",
                        Body = "This is a test message",
                        IsBodyHtml = false
                    };
                    mailMessage.To.Add("nanosoft.africa@gmail.com");

                    client.Send(mailMessage);
                    Console.WriteLine("✅ Email sent successfully!");
                }
            }
            catch (SmtpException ex)
            {
                Console.WriteLine($"❌ SMTP Error: {ex.Message}");
                Console.WriteLine($"Status Code: {ex.StatusCode}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ General Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }
    }
}
