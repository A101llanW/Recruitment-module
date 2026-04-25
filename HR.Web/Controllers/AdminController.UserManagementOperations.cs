using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        private ActionResult BuildGlobalUserManagementView()
        {
            var allUsers = _uow.Users.GetAll(u => u.Company, u => u.RoleDefinition).ToList();
            var allLastLogins = GetLatestLoginByUsername();
            var viewModel = new SuperAdminUserManagementViewModel
            {
                GlobalUsers = new List<UserManagementViewModel>(),
                Admins = new List<UserManagementViewModel>(),
                UsersByCompany = new Dictionary<string, List<UserManagementViewModel>>()
            };

            foreach (var user in allUsers)
            {
                var userVm = BuildSuperAdminUserVm(user, allLastLogins);
                AddUserToGlobalBuckets(viewModel, user, userVm);
            }

            return View("GlobalUserManagement", viewModel);
        }

        private Dictionary<string, AuditLog> GetLatestLoginByUsername()
        {
            return _uow.AuditLogs.GetAll()
                .Where(a => a.Action == "LOGIN_SUCCESS")
                .ToList()
                .GroupBy(a => a.Username)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Timestamp).First());
        }

        private UserManagementViewModel BuildSuperAdminUserVm(User user, Dictionary<string, AuditLog> allLastLogins)
        {
            allLastLogins.TryGetValue(user.UserName, out var lastLogin);
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
                IsLocked = _securityService.IsAccountLocked(user.UserName),
                LastLoginDate = lastLogin != null ? (DateTime?)lastLogin.Timestamp : null,
                LastLoginIP = lastLogin != null ? lastLogin.IPAddress : null,
                CreatedDate = DateTime.Now
            };
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
                return RedirectToAction("GlobalUserManagement");
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

            return RedirectToAction("GlobalUserManagement");
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
