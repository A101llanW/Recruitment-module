using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.ViewModels
{
    public class SecurityLogsViewModel
    {
        public List<LoginAttemptLog> LoginAttempts { get; set; }
        public List<AuditLogEntry> AuditLogs { get; set; }
        public LogFilter Filter { get; set; }
        public int TotalLoginAttempts { get; set; }
        public int TotalAuditLogs { get; set; }
        public int FailedLoginAttempts { get; set; }
        public int SuccessfulLogins { get; set; }
    }

    public class LoginAttemptLog
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string IPAddress { get; set; }
        public DateTime AttemptTime { get; set; }
        public bool WasSuccessful { get; set; }
        public string FailureReason { get; set; }
        public string StatusClass
        {
            get
            {
                return WasSuccessful ? "text-success" : "text-danger";
            }
        }
        public string StatusIcon
        {
            get
            {
                return WasSuccessful ? "fa-check-circle" : "fa-times-circle";
            }
        }
    }

    public class AuditLogEntry
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Action { get; set; }
        public string Controller { get; set; }
        public string EntityId { get; set; }
        public string IPAddress { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserAgent { get; set; }
        public bool WasSuccessful { get; set; }
        public string ErrorMessage { get; set; }
        public string StatusClass
        {
            get
            {
                return WasSuccessful ? "text-success" : "text-danger";
            }
        }
        public string StatusIcon
        {
            get
            {
                return WasSuccessful ? "fa-check-circle" : "fa-times-circle";
            }
        }
    }

    public class LogFilter
    {
        public LogFilter()
        {
            LogType = LogType.All;
        }

        public string Username { get; set; }
        public string Action { get; set; }
        public string Controller { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool? WasSuccessful { get; set; }
        public string IPAddress { get; set; }
        public LogType LogType { get; set; }
    }

    public enum LogType
    {
        All = 0,
        LoginAttempts = 1,
        AuditLogs = 2
    }
}
