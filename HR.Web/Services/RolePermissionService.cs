using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Services
{
    public class RolePermissionService
    {
        private static readonly string CurrentUserContextSlot = typeof(RolePermissionService).FullName + ".CurrentUserContext";

        private sealed class CurrentUserAccessContext
        {
            public bool IsAuthenticated { get; set; }
            public bool IsImpersonating { get; set; }
            public bool IsActualSuperAdmin { get; set; }
            public bool IsFullCompanyAdmin { get; set; }
            public bool HasCustomAdminRole { get; set; }
            public string BaseRole { get; set; }
            public int? CompanyId { get; set; }
            public int? RoleDefinitionId { get; set; }
            public IDictionary<string, string> PermissionMap { get; set; }
        }

        public bool CanCurrentUserAccessModule(string moduleKey, string requiredAccessLevel)
        {
            if (string.IsNullOrWhiteSpace(moduleKey))
            {
                return true;
            }

            var context = GetCurrentUserContext();
            if (!context.IsAuthenticated || context.IsImpersonating || context.IsActualSuperAdmin)
            {
                return true;
            }

            if (!string.Equals(context.BaseRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!context.HasCustomAdminRole)
            {
                return true;
            }

            if (context.PermissionMap == null || !context.PermissionMap.ContainsKey(moduleKey))
            {
                return false;
            }

            return MeetsAccessRequirement(context.PermissionMap[moduleKey], requiredAccessLevel);
        }

        public bool CanCurrentUserManageRoleDefinitions()
        {
            var context = GetCurrentUserContext();
            return context.IsImpersonating || context.IsActualSuperAdmin || context.IsFullCompanyAdmin;
        }

        public bool HasCurrentUserCustomAdminRole()
        {
            return GetCurrentUserContext().HasCustomAdminRole;
        }

        public bool IsCurrentUserReadOnlyForModule(string moduleKey)
        {
            if (string.IsNullOrWhiteSpace(moduleKey))
            {
                return false;
            }

            var context = GetCurrentUserContext();
            if (!context.IsAuthenticated || context.IsImpersonating || context.IsActualSuperAdmin)
            {
                return false;
            }

            if (!string.Equals(context.BaseRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!context.HasCustomAdminRole || context.PermissionMap == null)
            {
                return false;
            }

            string grantedAccessLevel;
            if (!context.PermissionMap.TryGetValue(moduleKey, out grantedAccessLevel))
            {
                return false;
            }

            return MeetsAccessRequirement(grantedAccessLevel, RoleAccessLevels.View) &&
                   !MeetsAccessRequirement(grantedAccessLevel, RoleAccessLevels.Manage);
        }

        public string GetDisplayRole(User user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) && user.RoleDefinition != null)
            {
                return user.RoleDefinition.Name;
            }

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) && user.RoleDefinitionId.HasValue)
            {
                using (var uow = new UnitOfWork())
                {
                    var role = uow.Context.RoleDefinitions.FirstOrDefault(r => r.Id == user.RoleDefinitionId.Value);
                    if (role != null)
                    {
                        return role.Name;
                    }
                }
            }

            return string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role;
        }

        public bool IsCustomAdminRole(User user)
        {
            return user != null &&
                   string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                   user.RoleDefinitionId.HasValue;
        }

        public bool IsFullCompanyAdmin(User user)
        {
            return user != null &&
                   string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                   user.CompanyId.HasValue &&
                   !user.RoleDefinitionId.HasValue;
        }

        private static bool MeetsAccessRequirement(string grantedAccessLevel, string requiredAccessLevel)
        {
            var grantedRank = GetAccessRank(grantedAccessLevel);
            var requiredRank = GetAccessRank(requiredAccessLevel);
            return grantedRank >= requiredRank;
        }

        private static int GetAccessRank(string accessLevel)
        {
            if (string.Equals(accessLevel, RoleAccessLevels.Manage, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (string.Equals(accessLevel, RoleAccessLevels.View, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 0;
        }

        private CurrentUserAccessContext GetCurrentUserContext()
        {
            var httpContext = HttpContext.Current;
            if (httpContext == null)
            {
                return new CurrentUserAccessContext();
            }

            if (httpContext.Items[CurrentUserContextSlot] is CurrentUserAccessContext cachedContext)
            {
                return cachedContext;
            }

            var context = BuildCurrentUserContext(httpContext);
            httpContext.Items[CurrentUserContextSlot] = context;
            return context;
        }

        private static CurrentUserAccessContext BuildCurrentUserContext(HttpContext httpContext)
        {
            var principal = httpContext.User;
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
            {
                return new CurrentUserAccessContext();
            }

            if (httpContext.Session != null && httpContext.Session["ImpersonatedCompanyId"] != null)
            {
                var impersonatedCompanyId = httpContext.Session["ImpersonatedCompanyId"] is int value
                    ? (int?)value
                    : null;
                return new CurrentUserAccessContext
                {
                    IsAuthenticated = true,
                    IsImpersonating = true,
                    BaseRole = "Admin",
                    CompanyId = impersonatedCompanyId
                };
            }

            var username = principal.Identity.Name ?? string.Empty;
            var lowerUsername = username.ToLower();
            var authenticatedCompanyId = httpContext.Items["AuthenticatedCompanyId"] as int?;

            using (var uow = new UnitOfWork())
            {
                var users = uow.Context.Users
                    .Include(u => u.RoleDefinition.RolePermissions)
                    .AsQueryable();

                User user = null;
                if (authenticatedCompanyId.HasValue)
                {
                    user = users.FirstOrDefault(u => u.CompanyId == authenticatedCompanyId.Value && u.UserName.ToLower() == lowerUsername);
                }
                else
                {
                    user = users.FirstOrDefault(u => !u.CompanyId.HasValue && u.UserName.ToLower() == lowerUsername);
                }

                if (user == null)
                {
                    user = users.FirstOrDefault(u => u.UserName.ToLower() == lowerUsername);
                }

                if (user == null)
                {
                    return new CurrentUserAccessContext
                    {
                        IsAuthenticated = true
                    };
                }

                var isActualSuperAdmin = !user.CompanyId.HasValue &&
                    (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(user.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase));

                var permissionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (user.RoleDefinition != null && user.RoleDefinition.IsActive)
                {
                    foreach (var permission in user.RoleDefinition.RolePermissions)
                    {
                        permissionMap[permission.ModuleKey] = permission.AccessLevel;
                    }
                }

                return new CurrentUserAccessContext
                {
                    IsAuthenticated = true,
                    IsActualSuperAdmin = isActualSuperAdmin,
                    IsFullCompanyAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                                         user.CompanyId.HasValue &&
                                         !user.RoleDefinitionId.HasValue,
                    HasCustomAdminRole = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                                         user.RoleDefinitionId.HasValue,
                    BaseRole = string.IsNullOrWhiteSpace(user.Role) ? "Client" : user.Role,
                    CompanyId = user.CompanyId,
                    RoleDefinitionId = user.RoleDefinitionId,
                    PermissionMap = permissionMap
                };
            }
        }
    }
}
