using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using HR.Web.Helpers;

namespace HR.Web.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body);

        Task SendAsync(string to, string subject, string body, IEnumerable<string> ccRecipients);
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
            _fromName = _settingsService.GetSetting("FromName") ?? ConfigurationManager.AppSettings["FromName"] ?? AppConfig.ProductName;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            await SendAsync(to, subject, body, null);
        }

        public async Task SendAsync(string to, string subject, string body, IEnumerable<string> ccRecipients)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            try
            {
                await SendMailCoreAsync(to, subject, body, ccRecipients);
            }
            catch (Exception ex)
            {
                LogEmailFailure(to, ex);
            }
        }

        private async Task SendMailCoreAsync(string to, string subject, string body, IEnumerable<string> ccRecipients)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            var recipient = to.Trim();
            var messageSubject = subject ?? string.Empty;
            var messageBody = body ?? string.Empty;

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
                    Subject = messageSubject,
                    Body = messageBody,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipient);

                if (ccRecipients != null)
                {
                    foreach (var cc in ccRecipients.Where(e => !string.IsNullOrWhiteSpace(e)))
                    {
                        mailMessage.CC.Add(cc.Trim());
                    }
                }

                await client.SendMailAsync(mailMessage);
            }
        }

        private async Task SendCriticalAsync(string to, string subject, string body)
        {
            try
            {
                await SendMailCoreAsync(to, subject, body, null);
            }
            catch (Exception ex)
            {
                LogEmailFailure(to, ex);
                throw;
            }
        }

        private static void LogEmailFailure(string to, Exception ex)
        {
            var recipient = to ?? string.Empty;
            if (ex == null)
            {
                System.Diagnostics.Debug.WriteLine("Email sending failed to " + recipient + ": unknown error");
                System.Diagnostics.Trace.WriteLine("Email sending failed to " + recipient + ": unknown error");
                return;
            }

            var error = ex;
            try
            {
                string logPath = AppDomain.CurrentDomain.BaseDirectory + "email_errors.txt";
                string logMessage = string.Format("[{0}] ERROR sending to {1}: {2}{3}Stack: {4}{3}",
                    DateTime.Now, recipient, error.Message, Environment.NewLine, error.StackTrace);
                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch (Exception)
            {
                // Best-effort local log write only; email failure is already traced below.
            }

            System.Diagnostics.Debug.WriteLine("Email sending failed: " + error.Message);
            System.Diagnostics.Trace.WriteLine("Email sending failed: " + error.Message);
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            var link = resetLink ?? string.Empty;
            var subject = "Password Reset Request - " + AppConfig.ProductName;
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
            <h2>{1}</h2>
            <p>Password Reset Request</p>
        </div>
        <div class='content'>
            <p>Hello,</p>
            <p>We received a request to reset your password for your {1} account.</p>
            
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
            <p>&copy; {2} {3}. All rights reserved.</p>
            <p>This is an automated message, please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>", link, AppConfig.ProductName, DateTime.UtcNow.Year, AppConfig.PublisherName);

            await SendAsync(to, subject, body);
        }
        public async Task SendMfaCodeEmailAsync(string to, string code)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            var verificationCode = code ?? string.Empty;
            var subject = "Your Verification Code - " + AppConfig.ProductName;
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
            <h2>{1}</h2>
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
            <p>&copy; {2} {3}. All rights reserved.</p>
        </div>
    </div>
</body>
</html>", verificationCode, AppConfig.ProductName, DateTime.UtcNow.Year, AppConfig.PublisherName);

            LogSensitiveCodeForDevelopment("MFA CODE", to, verificationCode, "mfa_codes.txt");

            await SendCriticalAsync(to, subject, body);
        }

        public async Task SendEmailVerificationOtpAsync(string to, string code)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            var verificationCode = code ?? string.Empty;
            var subject = "Email Verification - " + AppConfig.ProductName;
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
            <h2>{1}</h2>
            <p>Verify Your Email Address</p>
        </div>
        <div class='content'>
            <p>Thank you for using {1}. To complete your email verification, please use the following one-time password (OTP):</p>
            
            <div class='code'>{0}</div>
            
            <p>This code will expire in 15 minutes.</p>
            <p>If you did not request this, please ignore this email.</p>
        </div>
        <div class='footer'>
            <p>&copy; {2} {3}. All rights reserved.</p>
        </div>
    </div>
</body>
</html>", verificationCode, AppConfig.ProductName, DateTime.UtcNow.Year, AppConfig.PublisherName);

            LogSensitiveCodeForDevelopment("EMAIL VERIFICATION OTP", to, verificationCode, "verification_codes.txt");

            await SendCriticalAsync(to, subject, body);
        }

        private static void LogSensitiveCodeForDevelopment(string label, string to, string code, string fileName)
        {
            DevDiagnostics.LogOneTimeCode(label, to, code);

            if (!DevDiagnostics.IsEnabled())
            {
                return;
            }

            try
            {
                string logPath = AppDomain.CurrentDomain.BaseDirectory + fileName;
                string logMessage = string.Format("[{0}] {1} for {2}: {3}{4}", DateTime.Now, label, to, code, Environment.NewLine);
                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch (Exception)
            {
                // Best-effort development-only OTP log write; primary delivery already attempted above.
            }
        }
    }
}



























