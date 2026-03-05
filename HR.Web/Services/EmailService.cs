using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace HR.Web.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body);
        Task SendPasswordResetEmailAsync(string to, string resetLink);
        Task SendMfaCodeEmailAsync(string to, string code);
        Task SendEmailVerificationOtpAsync(string to, string code);
    }

    public class EmailService : IEmailService
    {
        private readonly ISettingsService _settingsService;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly bool _enableSsl;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService() : this(new SettingsService()) { }

        public EmailService(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            // Prioritize Database Settings, fall back to Web.config
            _smtpHost = _settingsService.GetSetting("SmtpHost") ?? ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = _settingsService.GetSetting<int>("SmtpPort", int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587"));
            _smtpUser = _settingsService.GetSetting("SmtpUser") ?? ConfigurationManager.AppSettings["SmtpUser"] ?? "";
            _smtpPass = _settingsService.GetSetting("SmtpPassword") ?? ConfigurationManager.AppSettings["SmtpPassword"] ?? "";
            _enableSsl = _settingsService.GetSetting<bool>("SmtpEnableSsl", bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"] ?? "true"));
            _fromEmail = _settingsService.GetSetting("FromEmail") ?? ConfigurationManager.AppSettings["FromEmail"] ?? "noreply@nanosoft.com";
            _fromName = _settingsService.GetSetting("FromName") ?? ConfigurationManager.AppSettings["FromName"] ?? "Nanosoft HR System";
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            try
            {
                using (var client = new SmtpClient(_smtpHost, _smtpPort))
                {
                    client.EnableSsl = _enableSsl;
                    client.UseDefaultCredentials = false;
                    
                    if (!string.IsNullOrEmpty(_smtpUser) || !string.IsNullOrEmpty(_smtpPass))
                    {
                        client.Credentials = new NetworkCredential(_smtpUser, _smtpPass);
                    }

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_fromEmail, _fromName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(to);

                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                // Log error to a file so we can see it
                try 
                {
                    string logPath = AppDomain.CurrentDomain.BaseDirectory + "email_errors.txt";
                    string logMessage = string.Format("[{0}] ERROR sending to {1}: {2}{3}Stack: {4}{3}", 
                        DateTime.Now, to, ex.Message, Environment.NewLine, ex.StackTrace);
                    System.IO.File.AppendAllText(logPath, logMessage);
                }
                catch { }
                
                System.Diagnostics.Debug.WriteLine("Email sending failed: " + ex.Message);
            }
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink)
        {
            var subject = "Password Reset Request - Nanosoft HR System";
            var body = string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Password Reset</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2c3e50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .button {{ display: inline-block; padding: 12px 24px; background: #3498db; color: white; text-decoration: none; border-radius: 4px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .security-note {{ background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; margin: 20px 0; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Nanosoft HR System</h2>
            <p>Password Reset Request</p>
        </div>
        <div class='content'>
            <p>Hello,</p>
            <p>We received a request to reset your password for your Nanosoft HR System account.</p>
            
            <div class='security-note'>
                <strong>Security Notice:</strong> This password reset link will expire in 24 hours for your security.
            </div>
            
            <p style='text-align: center;'>
                <a href='{0}' class='button'>Reset Your Password</a>
            </p>
            
            <p>If you didn't request this password reset, please ignore this email. Your password will remain unchanged.</p>
            
            <p>If the button above doesn't work, you can copy and paste this link into your browser:</p>
            <p style='word-break: break-all; background: #f0f0f0; padding: 10px; border-radius: 4px;'>{0}</p>
        </div>
        <div class='footer'>
            <p>&copy; 2026 Nanosoft Technologies. All rights reserved.</p>
            <p>This is an automated message, please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>", resetLink);

            await SendAsync(to, subject, body);
        }
        public async Task SendMfaCodeEmailAsync(string to, string code)
        {
            var subject = "Your Verification Code - Nanosoft HR System";
            var body = string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Verification Code</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2c3e50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; text-align: center; }}
        .code {{ display: inline-block; padding: 15px 30px; background: #eee; border: 1px dashed #3498db; font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #2c3e50; border-radius: 4px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .note {{ font-size: 14px; color: #e67e22; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Nanosoft HR System</h2>
            <p>Identity Verification</p>
        </div>
        <div class='content'>
            <p>Verification is required for your account access.</p>
            <p>Please enter the following 6-digit code on the login page:</p>
            
            <div class='code'>{0}</div>
            
            <p class='note'>This code will expire in 10 minutes.</p>
            <p>If you did not attempt to sign in, please secure your account immediately.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2026 Nanosoft Technologies. All rights reserved.</p>
        </div>
    </div>
</body>
</html>", code);

            // In development, log the code to the debug output so developers can see it without a real SMTP server
            System.Diagnostics.Trace.WriteLine(string.Format("--- [MFA CODE] Sent to {0}: {1} ---", to, code));
            System.Diagnostics.Debug.WriteLine(string.Format("--- [MFA CODE] Sent to {0}: {1} ---", to, code));
            
            // Also write to a local file in the app directory for easy access
            try 
            {
                string logPath = AppDomain.CurrentDomain.BaseDirectory + "mfa_codes.txt";
                string logMessage = string.Format("[{0}] MFA Code for {1}: {2}{3}", DateTime.Now, to, code, Environment.NewLine);
                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch { }

            await SendAsync(to, subject, body);
        }

        public async Task SendEmailVerificationOtpAsync(string to, string code)
        {
            var subject = "Email Verification - Nanosoft HR System";
            var body = string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Email Verification</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2c3e50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; text-align: center; }}
        .code {{ display: inline-block; padding: 15px 30px; background: #eee; border: 1px dashed #3498db; font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #2c3e50; border-radius: 4px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Nanosoft HR System</h2>
            <p>Verify Your Email Address</p>
        </div>
        <div class='content'>
            <p>Thank you for using Nanosoft HR. To complete your email verification, please use the following one-time password (OTP):</p>
            
            <div class='code'>{0}</div>
            
            <p>This code will expire in 15 minutes.</p>
            <p>If you did not request this, please ignore this email.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2026 Nanosoft Technologies. All rights reserved.</p>
        </div>
    </div>
</body>
</html>", code);

            // Log the code to a file for easy access during development/troubleshooting
            try 
            {
                string logPath = AppDomain.CurrentDomain.BaseDirectory + "verification_codes.txt";
                string logMessage = string.Format("[{0}] Email Verification Code for {1}: {2}{3}", DateTime.Now, to, code, Environment.NewLine);
                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch { }

            await SendAsync(to, subject, body);
        }
    }
}



























