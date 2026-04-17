using System;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        private ActionResult HandleSecurityLogs(LogFilter filter)
        {
            var normalizedFilter = NormalizeSecurityLogsFilter(filter);
            var viewModel = new SecurityLogsViewModel { Filter = normalizedFilter };

            var loginAttemptsQuery = BuildLoginAttemptsQuery(normalizedFilter);
            var loginAttempts = loginAttemptsQuery
                .OrderByDescending(l => l.AttemptTime)
                .Take(1000)
                .ToList();
            viewModel.LoginAttempts = loginAttempts.Select(l => new LoginAttemptLog
            {
                Id = l.Id,
                Username = l.Username,
                IPAddress = l.IPAddress,
                AttemptTime = l.AttemptTime,
                WasSuccessful = l.WasSuccessful,
                FailureReason = l.FailureReason
            }).ToList();

            var auditLogsQuery = BuildAuditLogsQuery(normalizedFilter);
            var auditLogs = auditLogsQuery
                .OrderByDescending(a => a.Timestamp)
                .Take(1000)
                .ToList();
            viewModel.AuditLogs = auditLogs.Select(a => new AuditLogEntry
            {
                Id = a.Id,
                Username = a.Username,
                Action = a.Action,
                Controller = a.Controller,
                EntityId = a.EntityId,
                IPAddress = a.IPAddress,
                Timestamp = a.Timestamp,
                UserAgent = a.UserAgent,
                WasSuccessful = a.WasSuccessful,
                ErrorMessage = a.ErrorMessage
            }).ToList();

            viewModel.TotalLoginAttempts = loginAttemptsQuery.Count();
            viewModel.TotalAuditLogs = auditLogsQuery.Count();
            viewModel.FailedLoginAttempts = loginAttemptsQuery.Count(l => !l.WasSuccessful);
            viewModel.SuccessfulLogins = loginAttemptsQuery.Count(l => l.WasSuccessful);

            return View(viewModel);
        }

        private LogFilter NormalizeSecurityLogsFilter(LogFilter filter)
        {
            var normalized = filter ?? new LogFilter();
            if (Request.QueryString.Count == 0)
            {
                normalized.Username = null;
                normalized.Action = null;
                normalized.Controller = null;
                return normalized;
            }

            if (string.Equals(normalized.Action, "SecurityLogs", StringComparison.OrdinalIgnoreCase))
            {
                normalized.Action = null;
            }

            if (string.Equals(normalized.Controller, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                normalized.Controller = null;
            }

            return normalized;
        }

        private IQueryable<LoginAttempt> BuildLoginAttemptsQuery(LogFilter filter)
        {
            var query = _tenantService.ApplyTenantFilter(_uow.LoginAttempts.GetAll().AsQueryable());
            if (!string.IsNullOrEmpty(filter.Username))
            {
                query = query.Where(l => l.Username.Contains(filter.Username));
            }

            if (!string.IsNullOrEmpty(filter.IPAddress))
            {
                query = query.Where(l => l.IPAddress.Contains(filter.IPAddress));
            }

            if (filter.WasSuccessful.HasValue)
            {
                query = query.Where(l => l.WasSuccessful == filter.WasSuccessful.Value);
            }

            if (filter.StartDate.HasValue)
            {
                query = query.Where(l => l.AttemptTime >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                var nextDay = filter.EndDate.Value.Date.AddDays(1);
                query = query.Where(l => l.AttemptTime < nextDay);
            }

            return query;
        }

        private IQueryable<AuditLog> BuildAuditLogsQuery(LogFilter filter)
        {
            var query = _tenantService.ApplyTenantFilter(_uow.AuditLogs.GetAll().AsQueryable());
            if (!string.IsNullOrEmpty(filter.Username))
            {
                query = query.Where(a => a.Username.Contains(filter.Username));
            }

            query = ApplyAuditActionFilter(query, filter.Action);

            if (!string.IsNullOrEmpty(filter.Controller))
            {
                query = query.Where(a => a.Controller.Contains(filter.Controller));
            }

            if (!string.IsNullOrEmpty(filter.IPAddress))
            {
                query = query.Where(a => a.IPAddress.Contains(filter.IPAddress));
            }

            query = ApplyAuditSuccessFilter(query, filter.WasSuccessful);
            query = ApplyAuditDateRangeFilter(query, filter.StartDate, filter.EndDate);

            return query;
        }

        private static IQueryable<AuditLog> ApplyAuditActionFilter(IQueryable<AuditLog> query, string action)
        {
            if (string.IsNullOrEmpty(action))
            {
                return query;
            }

            var actionMatch = action.ToLower();
            if (string.Equals(actionMatch, "securitylogs", StringComparison.OrdinalIgnoreCase))
            {
                return query;
            }

            return query.Where(a => a.Action.ToLower().Contains(actionMatch));
        }

        private static IQueryable<AuditLog> ApplyAuditSuccessFilter(IQueryable<AuditLog> query, bool? wasSuccessful)
        {
            if (!wasSuccessful.HasValue)
            {
                return query;
            }

            return query.Where(a => a.WasSuccessful == wasSuccessful.Value);
        }

        private static IQueryable<AuditLog> ApplyAuditDateRangeFilter(IQueryable<AuditLog> query, DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                var nextDay = endDate.Value.Date.AddDays(1);
                query = query.Where(a => a.Timestamp < nextDay);
            }

            return query;
        }
    }
}
