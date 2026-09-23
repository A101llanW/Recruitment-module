using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        /// <summary>
        /// Sends the user to global SuperAdmin user management when acting as platform admin,
        /// or tenant-scoped user management when impersonating / acting as a company admin.
        /// </summary>
        private ActionResult RedirectToUserManagementHome()
        {
            return _tenantService.IsSuperAdmin()
                ? RedirectToAction("GlobalUserManagement")
                : RedirectToAction("UserManagement");
        }

        private ActionResult BuildGlobalUserManagementView()
        {
            var allUsers = _uow.Users.GetAll(u => u.Company, u => u.RoleDefinition).ToList();
            var allLastLogins = LoadLatestSuccessfulLogins(allUsers.Select(u => u.UserName));
            var lockouts = _securityService.GetLockoutStates(allUsers);
            var viewModel = new SuperAdminUserManagementViewModel
            {
                GlobalUsers = new List<UserManagementViewModel>(),
                Admins = new List<UserManagementViewModel>(),
                UsersByCompany = new Dictionary<string, List<UserManagementViewModel>>()
            };

            foreach (var user in allUsers)
            {
                var userVm = BuildSuperAdminUserVm(user, allLastLogins, lockouts);
                AddUserToGlobalBuckets(viewModel, user, userVm);
            }

            return View("GlobalUserManagement", viewModel);
        }

        private Dictionary<string, LatestLoginSnapshot> LoadLatestSuccessfulLogins(IEnumerable<string> usernames)
        {
            var names = usernames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count == 0)
            {
                return new Dictionary<string, LatestLoginSnapshot>(StringComparer.OrdinalIgnoreCase);
            }

            return _uow.AuditLogs.GetAll()
                .Where(a => a.Action == "LOGIN_SUCCESS" && names.Contains(a.Username))
                .Select(a => new
                {
                    a.Username,
                    a.Timestamp,
                    a.IPAddress
                })
                .ToList()
                .GroupBy(a => a.Username, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var latest = g.OrderByDescending(x => x.Timestamp).First();
                        return new LatestLoginSnapshot
                        {
                            Username = latest.Username,
                            Timestamp = latest.Timestamp,
                            IPAddress = latest.IPAddress
                        };
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, string> LoadPhonesByEmail(IEnumerable<string> emails)
        {
            var addresses = emails
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (addresses.Count == 0)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return _uow.Applicants.GetAll()
                .Where(a => addresses.Contains(a.Email))
                .Select(a => new { a.Email, a.Phone })
                .ToList()
                .GroupBy(a => a.Email, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(a => a.Phone).FirstOrDefault(), StringComparer.OrdinalIgnoreCase);
        }

        private UserManagementViewModel BuildSuperAdminUserVm(
            User user,
            Dictionary<string, LatestLoginSnapshot> allLastLogins,
            Dictionary<int, UserLockoutState> lockouts)
        {
            allLastLogins.TryGetValue(user.UserName ?? string.Empty, out var lastLogin);
            if (!lockouts.TryGetValue(user.Id, out var lockout))
            {
                lockout = new UserLockoutState();
            }

            return new UserManagementViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName,
                Email = user.Email,
                Role = _rolePermissionService.GetDisplayRole(user),
                BaseRole = user.Role,
                CompanyName = user.Company != null ? user.Company.Name : "System",
                IsLocked = lockout.IsLocked,
                LockoutEndTime = lockout.LockoutEndTime,
                FailedLoginAttempts = lockout.FailedLoginAttempts,
                LastLoginDate = lastLogin != null ? (DateTime?)lastLogin.Timestamp : null,
                LastLoginIP = lastLogin != null ? lastLogin.IPAddress : null,
                CreatedDate = DateTime.Now
            };
        }

        private sealed class LatestLoginSnapshot
        {
            public string Username { get; set; }
            public DateTime Timestamp { get; set; }
            public string IPAddress { get; set; }
        }

        private static void AddUserToGlobalBuckets(SuperAdminUserManagementViewModel viewModel, User user, UserManagementViewModel userVm)
        {
            AddUserToGlobalRoleBuckets(viewModel, user, userVm);
            if (ShouldSkipCompanyBucket(user))
            {
                return;
            }

            AddUserToCompanyBucket(viewModel, user, userVm);
        }

        private static void AddUserToGlobalRoleBuckets(SuperAdminUserManagementViewModel viewModel, User user, UserManagementViewModel userVm)
        {
            if (IsSuperAdmin(user))
            {
                viewModel.GlobalUsers.Add(userVm);
            }

            if (IsCompanyAdmin(user))
            {
                viewModel.Admins.Add(userVm);
            }
        }

        private static bool ShouldSkipCompanyBucket(User user)
        {
            if (IsSuperAdmin(user) || IsCompanyAdmin(user))
            {
                return true;
            }

            return !user.CompanyId.HasValue;
        }

        private static bool IsSuperAdmin(User user)
        {
            return user.Role == "SuperAdmin";
        }

        private static bool IsCompanyAdmin(User user)
        {
            return user.Role == "Admin" && user.CompanyId.HasValue && !user.RoleDefinitionId.HasValue;
        }

        private static void AddUserToCompanyBucket(SuperAdminUserManagementViewModel viewModel, User user, UserManagementViewModel userVm)
        {
            var companyName = user.Company != null ? user.Company.Name : "Unknown";
            var companyUsers = GetOrCreateCompanyBucket(viewModel, companyName);
            companyUsers.Add(userVm);
        }

        private static List<UserManagementViewModel> GetOrCreateCompanyBucket(SuperAdminUserManagementViewModel viewModel, string companyName)
        {
            if (viewModel.UsersByCompany.TryGetValue(companyName, out var companyUsers))
            {
                return companyUsers;
            }

            companyUsers = new List<UserManagementViewModel>();
            viewModel.UsersByCompany[companyName] = companyUsers;
            return companyUsers;
        }

        private ActionResult HandleDeleteUser(int id)
        {
            var user = _uow.Users.Get(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            if (IsDeletingCurrentUser(user.UserName))
            {
                TempData["Message"] = "You cannot delete your own account.";
                return RedirectToUserManagementHome();
            }

            var deletedUsername = user.UserName;
            try
            {
                DeleteClientApplicantRecords(user);
                DeleteUserSecurityArtifacts(user);
                _uow.Context.Users.Remove(user);
                _uow.Complete();

                _auditService.LogAction(User.Identity.Name, "DELETE_USER", "UserManagement", id.ToString(), true,
                    string.Format("Permanently deleted user {0} and all associated records", deletedUsername));
                TempData["Message"] = string.Format("User {0} and all associated records have been permanently deleted.", deletedUsername);
            }
            catch (Exception ex)
            {
                TempData["Message"] = "Error deleting user: " + ex.Message;
            }

            return RedirectToUserManagementHome();
        }

        private bool IsDeletingCurrentUser(string targetUserName)
        {
            var currentUser = User.Identity.Name.ToLower();
            return targetUserName.ToLower() == currentUser;
        }

        private void DeleteClientApplicantRecords(User user)
        {
            if (user.Role != "Client" || string.IsNullOrEmpty(user.Email))
            {
                return;
            }

            var emailLower = user.Email.ToLower();
            var applicants = _uow.Context.Applicants.Where(a => a.Email.ToLower() == emailLower).ToList();
            foreach (var applicant in applicants)
            {
                DeleteApplicantApplicationGraph(applicant.Id);
                _uow.Context.Applicants.Remove(applicant);
            }
        }

        private void DeleteApplicantApplicationGraph(int applicantId)
        {
            var applications = _uow.Context.Applications.Where(app => app.ApplicantId == applicantId).ToList();
            foreach (var app in applications)
            {
                var answers = _uow.Context.ApplicationAnswers.Where(ans => ans.ApplicationId == app.Id);
                _uow.Context.ApplicationAnswers.RemoveRange(answers);

                var interviews = _uow.Context.Interviews.Where(i => i.ApplicationId == app.Id);
                _uow.Context.Interviews.RemoveRange(interviews);

                var onboardings = _uow.Context.Onboardings.Where(o => o.ApplicationId == app.Id);
                _uow.Context.Onboardings.RemoveRange(onboardings);

                _uow.Context.Applications.Remove(app);
            }
        }

        private void DeleteUserSecurityArtifacts(User user)
        {
            var impersonations = _uow.Context.ImpersonationRequests.Where(r => r.RequestedFrom == user.UserName || r.RequestedBy == user.UserName);
            _uow.Context.ImpersonationRequests.RemoveRange(impersonations);

            var resets = _uow.Context.PasswordResets.Where(p => p.UserId == user.Id);
            _uow.Context.PasswordResets.RemoveRange(resets);

            var loginAttempts = _uow.Context.LoginAttempts.Where(l => l.Username == user.UserName);
            _uow.Context.LoginAttempts.RemoveRange(loginAttempts);

            var auditLogs = _uow.Context.AuditLogs.Where(a => a.Username == user.UserName);
            _uow.Context.AuditLogs.RemoveRange(auditLogs);
        }
    }
}
