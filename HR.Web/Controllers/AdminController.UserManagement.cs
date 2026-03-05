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
        /// <summary>
        /// Global user management for SuperAdmins and Admins.
        /// Views: Global users (no company), all Admins, and users per company.
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        public ActionResult GlobalUserManagement()
        {
            // Verify if user is actually a global admin (SuperAdmin or Admin with no company)
            // if (!_tenantService.IsSuperAdmin()) 
            // {
            //     return new HttpStatusCodeResult(403, "Only SuperAdmins or Global Admins can access this view.");
            // }

            var allUsers = _uow.Users.GetAll(u => u.Company).ToList();
            
            // Optimization: Get last login for all users once
            var allLastLogins = _uow.AuditLogs.GetAll()
                .Where(a => a.Action == "LOGIN_SUCCESS")
                .ToList()
                .GroupBy(a => a.Username)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Timestamp).First());

            var viewModel = new SuperAdminUserManagementViewModel
            {
                GlobalUsers = new List<UserManagementViewModel>(),
                Admins = new List<UserManagementViewModel>(),
                UsersByCompany = new Dictionary<string, List<UserManagementViewModel>>()
            };

            foreach (var user in allUsers)
            {
                var lastLogin = allLastLogins.ContainsKey(user.UserName) ? allLastLogins[user.UserName] : null;
                var isLocked = _securityService.IsAccountLocked(user.UserName);

                var userVm = new UserManagementViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    UserName = user.UserName,
                    Email = user.Email,
                    Role = user.Role,
                    CompanyName = user.Company != null ? user.Company.Name : "System",
                    IsLocked = isLocked,
                    LastLoginDate = lastLogin != null ? (DateTime?)lastLogin.Timestamp : null,
                    LastLoginIP = lastLogin != null ? lastLogin.IPAddress : null,
                    CreatedDate = DateTime.Now // Placeholder
                };

                // 1. System Users: Only SuperAdmins
                if (user.Role == "SuperAdmin")
                {
                    viewModel.GlobalUsers.Add(userVm);
                }
                
                // 2. Global Admins: Company admins only (Admin role + has Company, excluding SuperAdmins)
                if (user.Role == "Admin" && user.CompanyId.HasValue)
                {
                    viewModel.Admins.Add(userVm);
                }

                // 3. Tenant Users: Clients only (no admins or superadmins)
                if (user.Role != "Admin" && user.Role != "SuperAdmin" && user.CompanyId.HasValue)
                {
                    var companyName = user.Company != null ? user.Company.Name : "Unknown";
                    if (!viewModel.UsersByCompany.ContainsKey(companyName))
                    {
                        viewModel.UsersByCompany[companyName] = new List<UserManagementViewModel>();
                    }
                    viewModel.UsersByCompany[companyName].Add(userVm);
                }
            }

            return View(viewModel);
        }

        /// <summary>
        /// Allows Admins and SuperAdmins to elevate any user to SuperAdmin role.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult ElevateToSuperAdmin(int id)
        {
            var user = _uow.Users.Get(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var oldRole = user.Role;
            user.Role = "SuperAdmin";
            _uow.Users.Update(user);
            _uow.Complete();

            // Log the elevation
            _auditService.LogAction(
                User.Identity.Name, 
                "ELEVATE_TO_SUPERADMIN", 
                "UserManagement", 
                user.Id.ToString(), 
                true, 
                string.Format("Elevated user {0} from {1} to SuperAdmin", user.UserName, oldRole)
            );

            TempData["Message"] = string.Format("User {0} has been elevated to SuperAdmin.", user.UserName);
            return RedirectToAction("GlobalUserManagement");
        }
    }
}
