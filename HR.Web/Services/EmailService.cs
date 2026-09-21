using System;
using System.Collections.Generic;
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
        Task SendAsync(string to, string subject, string body, int? companyId);
        Task SendAsync(string to, string subject, string body, IEnumerable<string> ccRecipients);
        Task SendAsync(string to, string subject, string body, IEnumerable<string> ccRecipients, int? companyId);
        Task SendPasswordResetEmailAsync(string to, string resetLink);
        Task SendPasswordResetEmailAsync(string to, string resetLink, int? companyId);
        Task SendMfaCodeEmailAsync(string to, string code);
        Task SendMfaCodeEmailAsync(string to, string code, int? companyId);
        Task SendEmailVerificationOtpAsync(string to, string code);
        Task SendEmailVerificationOtpAsync(string to, string code, int? companyId);
    }

    public class EmailService : IEmailService
    {
        private readonly ICompanySmtpSettingsService _companySmtpSettingsService;

        public EmailService()
            : this(new CompanySmtpSettingsService())
        {
        }

        public EmailService(ISettingsService settingsService)
            : this(new CompanySmtpSettingsService())
        {
            if (settingsService == null)
            {
                throw new ArgumentNullException(nameof(settingsService));
            }
        }

        public EmailService(ICompanySmtpSettingsService companySmtpSettingsService)
        {
            _companySmtpSettingsService = companySmtpSettingsService ?? throw new ArgumentNullException(nameof(companySmtpSettingsService));
        }

        public EmailService(ISettingsService settingsService, ICompanySmtpSettingsService companySmtpSettingsService)
            : this(companySmtpSettingsService)
        {
            if (settingsService == null)
            {
                throw new ArgumentNullException(nameof(settingsService));
            }
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            await SendAsync(to, subject, body, null, null);
        }

        public async Task SendAsync(string to, string subject, string body, int? companyId)
        {
            await SendAsync(to, subject, body, null, companyId);
        }

        public async Task SendAsync(string to, string subject, string body, IEnumerable<string> ccRecipients)
        {
            await SendAsync(to, subject, body, ccRecipients, null);
        }

        public async Task SendAsync(string to, string subject, string body, IEnumerable<string> ccRecipients, int? companyId)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            try
            {
                var smtpConfig = _companySmtpSettingsService.ResolveForCompany(companyId);
                await SendMailCoreAsync(to, subject, body, ccRecipients, smtpConfig);
            }
            catch (Exception ex)
            {
                LogEmailFailure(to, ex);
            }
        }

        private async Task SendMailCoreAsync(string to, string subject, string body, IEnumerable<string> ccRecipients, SmtpConfiguration smtpConfig)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            if (smtpConfig == null || !smtpConfig.IsUsable())
            {
                smtpConfig = _companySmtpSettingsService.ResolveGlobal();
            }

            var recipient = to.Trim();
            var messageSubject = subject ?? string.Empty;
            var messageBody = body ?? string.Empty;

            using (var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port))
            {
                client.EnableSsl = smtpConfig.EnableSsl;
                client.UseDefaultCredentials = false;
                client.Timeout = 10000;

                if (!string.IsNullOrEmpty(smtpConfig.User) || !string.IsNullOrEmpty(smtpConfig.Password))
                {
                    client.Credentials = new NetworkCredential(smtpConfig.User, smtpConfig.Password);
                }

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpConfig.FromEmail, smtpConfig.FromName),
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

                await Task.Factory.StartNew(() => client.Send(mailMessage));
            }
        }

        private async Task SendCriticalAsync(string to, string subject, string body, int? companyId)
        {
            try
            {
                var smtpConfig = _companySmtpSettingsService.ResolveForCompany(companyId);
                await SendMailCoreAsync(to, subject, body, null, smtpConfig);
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
            await SendPasswordResetEmailAsync(to, resetLink, null);
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink, int? companyId)
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

            await SendAsync(to, subject, body, companyId);
        }

        public async Task SendMfaCodeEmailAsync(string to, string code)
        {
            await SendMfaCodeEmailAsync(to, code, null);
        }

        public async Task SendMfaCodeEmailAsync(string to, string code, int? companyId)
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

            await SendCriticalAsync(to, subject, body, companyId);
        }

        public async Task SendEmailVerificationOtpAsync(string to, string code)
        {
            await SendEmailVerificationOtpAsync(to, code, null);
        }

        public async Task SendEmailVerificationOtpAsync(string to, string code, int? companyId)
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

            await SendCriticalAsync(to, subject, body, companyId);
        }

        private static void LogSensitiveCodeForDevelopment(string label, string to, string code, string fileName)
        {
            var recipient = to ?? string.Empty;
            var oneTimeCode = code ?? string.Empty;
            DevDiagnostics.LogOneTimeCode(label ?? string.Empty, recipient, oneTimeCode);

            if (!DevDiagnostics.IsEnabled())
            {
                return;
            }

            try
            {
                string logPath = AppDomain.CurrentDomain.BaseDirectory + fileName;
                string logMessage = string.Format("[{0}] {1} for {2}: {3}{4}", DateTime.Now, label ?? string.Empty, recipient, oneTimeCode, Environment.NewLine);
                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch (Exception)
            {
                // Best-effort development-only OTP log write; primary delivery already attempted above.
            }
        }
    }
}
