using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using HR.Web.Models;
using MvcUrlHelper = System.Web.Mvc.UrlHelper;

namespace HR.Web.Services
{
    public sealed class NavMenuItemModel
    {
        public string Label { get; set; }
        public string Url { get; set; }
        public string IconClass { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class NavMenuGroupModel
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string DropdownId { get; set; }
        public IList<NavMenuItemModel> Items { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class NavBrandModel
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string LogoUrl { get; set; }
        public bool IsTenantBranded { get; set; }
    }

    public sealed class NavUserIdentityModel
    {
        public string DisplayName { get; set; }
        public string Initials { get; set; }
        public string RoleBadge { get; set; }
        public string RoleBadgeClass { get; set; }
        public bool ShowAccountMenu { get; set; }
    }

    public sealed class NavMenuModel
    {
        public NavBrandModel Brand { get; set; }
        public NavUserIdentityModel UserIdentity { get; set; }
        public NavMenuItemModel HomeLink { get; set; }
        public IList<NavMenuGroupModel> Groups { get; set; }
        public NavMenuGroupModel SystemGroup { get; set; }
        public bool UseGroupedMenus { get; set; }
        public bool ShowImpersonationChip { get; set; }
        public string ImpersonatedCompanyName { get; set; }
        public string StopImpersonatingUrl { get; set; }
        public string TenantToken { get; set; }
        public bool IsAuthenticated { get; set; }
        public string LoginUrl { get; set; }
        public string RegisterUrl { get; set; }
        public string ProfileUrl { get; set; }
        public string ChangePasswordUrl { get; set; }
        public string LogoutUrl { get; set; }
    }

    public class NavMenuBuilder
    {
        private readonly RolePermissionService _rolePermissionService;

        public NavMenuBuilder()
            : this(new RolePermissionService())
        {
        }

        public NavMenuBuilder(RolePermissionService rolePermissionService)
        {
            _rolePermissionService = rolePermissionService ?? new RolePermissionService();
        }

        public NavMenuModel Build(
            HttpContextBase httpContext,
            RequestContext requestContext,
            Company tenantContext,
            string tenantToken,
            string currentController,
            string currentAction,
            string currentModuleKey)
        {
            var url = new MvcUrlHelper(requestContext);
            var user = httpContext?.User;
            bool isAuthenticated = user != null && user.Identity != null && user.Identity.IsAuthenticated;
            bool isSuperAdminUser = isAuthenticated && user.IsInRole("SuperAdmin");
            bool isAdminUser = isAuthenticated && user.IsInRole("Admin");
            bool isClientUser = isAuthenticated && !isSuperAdminUser && !isAdminUser;
            bool isImpersonating = httpContext?.Session != null && httpContext.Session["ImpersonatedCompanyId"] != null;

            bool canViewPositions = _rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Positions, RoleAccessLevels.View);
            bool canViewApplications = _rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Applications, RoleAccessLevels.View);
            bool canViewInterviews = _rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Interviews, RoleAccessLevels.View);
            bool canViewQuestions = _rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Questions, RoleAccessLevels.View);
            bool canViewApplicants = _rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Applicants, RoleAccessLevels.View);
            bool canViewUsers = _rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.UserManagement, RoleAccessLevels.View);
            bool canViewSecurityLogs = _rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.SecurityLogs, RoleAccessLevels.View);
            bool canViewReports = _rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Reports, RoleAccessLevels.View);
            bool canViewDepartments = _rolePermissionService.CanCurrentUserAccessModule(RoleModuleCatalog.Departments, RoleAccessLevels.View);
            bool canManageRoleTemplates = _rolePermissionService.CanCurrentUserManageRoleDefinitions();

            var model = new NavMenuModel
            {
                TenantToken = tenantToken,
                IsAuthenticated = isAuthenticated,
                Brand = BuildBrand(url, tenantContext, tenantToken, isAuthenticated, isSuperAdminUser, isAdminUser, isImpersonating),
                UserIdentity = BuildUserIdentity(user, isAuthenticated, isSuperAdminUser, isAdminUser, isClientUser),
                LoginUrl = url.Action("Login", "Account", new { tenant = tenantToken }),
                RegisterUrl = url.Action("Register", "Account", new { tenant = tenantToken }),
                ProfileUrl = url.Action("Profile", "Account", new { tenant = tenantToken }),
                ChangePasswordUrl = url.Action("ChangePassword", "Account", new { tenant = tenantToken }),
                LogoutUrl = url.Action("Logout", "Account", new { tenant = tenantToken }),
                ShowImpersonationChip = isImpersonating,
                ImpersonatedCompanyName = isImpersonating
                    ? (httpContext.Session["ImpersonatedCompanyName"] as string ?? tenantContext?.Name ?? "Tenant")
                    : null,
                StopImpersonatingUrl = url.Action("StopImpersonating", "Companies"),
                UseGroupedMenus = isAuthenticated && !isClientUser,
                Groups = new List<NavMenuGroupModel>()
            };

            if (isAuthenticated && !isClientUser)
            {
                model.HomeLink = BuildHomeLink(url, tenantToken, isSuperAdminUser, isAdminUser, isImpersonating, currentController);
            }

            if (isSuperAdminUser && !isImpersonating)
            {
                model.SystemGroup = BuildSystemGroup(url, tenantToken, currentController, currentAction);
            }

            if (isClientUser)
            {
                AddClientFlatItems(model, url, tenantToken, currentController, canViewPositions, canViewApplications);
            }
            else
            {
                BuildGroupedMenus(
                    model,
                    url,
                    tenantToken,
                    currentController,
                    currentAction,
                    currentModuleKey,
                    isSuperAdminUser,
                    isAdminUser,
                    isImpersonating,
                    canViewPositions,
                    canViewApplications,
                    canViewInterviews,
                    canViewQuestions,
                    canViewApplicants,
                    canViewUsers,
                    canViewSecurityLogs,
                    canViewReports,
                    canViewDepartments,
                    canManageRoleTemplates);
            }

            return model;
        }

        private static NavBrandModel BuildBrand(
            MvcUrlHelper url,
            Company tenantContext,
            string tenantToken,
            bool isAuthenticated,
            bool isSuperAdminUser,
            bool isAdminUser,
            bool isImpersonating)
        {
            string brandUrl = url.Action("Index", "Positions", new { tenant = tenantToken });
            if (isAuthenticated)
            {
                if (isImpersonating)
                {
                    brandUrl = url.Action("Index", "Dashboard", new { tenant = tenantToken });
                }
                else if (isSuperAdminUser)
                {
                    brandUrl = url.Action("Index", "Companies", new { tenant = tenantToken });
                }
                else if (isAdminUser)
                {
                    brandUrl = url.Action("Index", "Dashboard", new { tenant = tenantToken });
                }
            }

            if (tenantContext != null)
            {
                var logoUrl = ResolveCompanyLogoUrl(tenantContext.LogoPath, url);
                var hasName = !string.IsNullOrWhiteSpace(tenantContext.Name);
                var hasLogo = !string.IsNullOrEmpty(logoUrl);

                return new NavBrandModel
                {
                    Url = brandUrl,
                    Title = hasName ? tenantContext.Name.Trim() : HR.Web.Helpers.AppConfig.ProductName,
                    Subtitle = hasLogo && hasName ? null : HR.Web.Helpers.AppConfig.ProductName,
                    LogoUrl = logoUrl,
                    IsTenantBranded = true
                };
            }

            return new NavBrandModel
            {
                Url = brandUrl,
                Title = HR.Web.Helpers.AppConfig.ProductName,
                Subtitle = HR.Web.Helpers.AppConfig.PublisherName,
                LogoUrl = url.Content("~/Content/images/nanosoft-logo-transparent.png"),
                IsTenantBranded = false
            };
        }

        public static string ResolveCompanyLogoUrl(string logoPath, MvcUrlHelper url)
        {
            if (string.IsNullOrWhiteSpace(logoPath) || url == null)
            {
                return null;
            }

            var path = logoPath.Trim().Replace('\\', '/');
            if (path.StartsWith("~/", StringComparison.Ordinal))
            {
                return url.Content(path);
            }

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                return url.Content("~" + path);
            }

            return url.Content("~/" + path.TrimStart('/'));
        }

        private static NavUserIdentityModel BuildUserIdentity(
            System.Security.Principal.IPrincipal user,
            bool isAuthenticated,
            bool isSuperAdminUser,
            bool isAdminUser,
            bool isClientUser)
        {
            if (!isAuthenticated)
            {
                return new NavUserIdentityModel { ShowAccountMenu = false };
            }

            var displayName = FormatDisplayName(user.Identity.Name);
            return new NavUserIdentityModel
            {
                DisplayName = displayName,
                Initials = GetInitials(displayName),
                RoleBadge = GetRoleBadgeLabel(isSuperAdminUser, isAdminUser, isClientUser),
                RoleBadgeClass = GetRoleBadgeClass(isSuperAdminUser, isAdminUser, isClientUser),
                ShowAccountMenu = true
            };
        }

        private static NavMenuItemModel BuildHomeLink(
            MvcUrlHelper url,
            string tenantToken,
            bool isSuperAdminUser,
            bool isAdminUser,
            bool isImpersonating,
            string currentController)
        {
            string homeController;
            string homeAction = "Index";
            string label;

            if (isImpersonating || isAdminUser)
            {
                homeController = "Dashboard";
                label = "Dashboard";
            }
            else if (isSuperAdminUser)
            {
                homeController = "Companies";
                label = "Home";
            }
            else
            {
                homeController = "Positions";
                label = "Home";
            }

            return new NavMenuItemModel
            {
                Label = label,
                Url = url.Action(homeAction, homeController, new { tenant = tenantToken }),
                IconClass = "fas fa-home",
                IsActive = string.Equals(currentController, homeController, StringComparison.OrdinalIgnoreCase)
            };
        }

        private static void AddClientFlatItems(
            NavMenuModel model,
            MvcUrlHelper url,
            string tenantToken,
            string currentController,
            bool canViewPositions,
            bool canViewApplications)
        {
            var flat = new List<NavMenuItemModel>();
            if (canViewPositions)
            {
                flat.Add(new NavMenuItemModel
                {
                    Label = "Positions",
                    Url = url.Action("Index", "Positions", new { tenant = tenantToken }),
                    IconClass = "fas fa-sitemap",
                    IsActive = string.Equals(currentController, "Positions", StringComparison.OrdinalIgnoreCase)
                });
            }

            if (canViewApplications)
            {
                flat.Add(new NavMenuItemModel
                {
                    Label = "Applications",
                    Url = url.Action("Index", "Applications", new { tenant = tenantToken }),
                    IconClass = "fas fa-file-alt",
                    IsActive = string.Equals(currentController, "Applications", StringComparison.OrdinalIgnoreCase)
                });
            }

            if (flat.Count > 0)
            {
                model.Groups.Add(new NavMenuGroupModel
                {
                    Key = "client-flat",
                    Label = null,
                    Items = flat
                });
            }
        }

        private static void BuildGroupedMenus(
            NavMenuModel model,
            MvcUrlHelper url,
            string tenantToken,
            string currentController,
            string currentAction,
            string currentModuleKey,
            bool isSuperAdminUser,
            bool isAdminUser,
            bool isImpersonating,
            bool canViewPositions,
            bool canViewApplications,
            bool canViewInterviews,
            bool canViewQuestions,
            bool canViewApplicants,
            bool canViewUsers,
            bool canViewSecurityLogs,
            bool canViewReports,
            bool canViewDepartments,
            bool canManageRoleTemplates)
        {
            var hireItems = new List<NavMenuItemModel>();
            if (!(isSuperAdminUser && !isImpersonating) && (!isAdminUser || canViewPositions || isImpersonating))
            {
                hireItems.Add(CreateItem(url, tenantToken, "Positions", "Positions", "Index", "fas fa-sitemap",
                    IsControllerActive(currentController, "Positions")));
            }

            if (!isSuperAdminUser || isImpersonating)
            {
                if (!isAdminUser || canViewApplications || isImpersonating)
                {
                    hireItems.Add(CreateItem(url, tenantToken, "Applications", "Applications", "Index", "fas fa-file-alt",
                        IsControllerActive(currentController, "Applications")));
                }

                if (!isAdminUser || canViewInterviews || isImpersonating)
                {
                    hireItems.Add(CreateItem(url, tenantToken, "Interviews", "Interviews", "Index", "fas fa-comments",
                        IsControllerActive(currentController, "Interviews")));
                }
            }

            if (isAdminUser || isImpersonating)
            {
                if (canViewApplicants || isImpersonating)
                {
                    hireItems.Add(CreateItem(url, tenantToken, "Applicants", "Applicants", "Index", "fas fa-users",
                        IsControllerActive(currentController, "Applicants")));
                }
            }

            AddGroup(model, "hire", "Hire", "navHireMenu", hireItems);

            var assessItems = new List<NavMenuItemModel>();
            if (isAdminUser || isImpersonating)
            {
                if (canViewQuestions || isImpersonating)
                {
                    assessItems.Add(CreateItem(url, tenantToken, "Questions", "Admin", "Questions", "fas fa-question-circle",
                        IsQuestionsModuleActive(currentController, currentAction, currentModuleKey)));
                }
            }

            AddGroup(model, "assess", "Assess", "navAssessMenu", assessItems);

            var adminItems = new List<NavMenuItemModel>();
            if (isAdminUser || isImpersonating)
            {
                if (canViewUsers || isImpersonating)
                {
                    adminItems.Add(CreateItem(url, tenantToken, "User Management", "Admin", "UserManagement", "fas fa-user-shield",
                        IsAdminUserManagementActive(currentController, currentAction)));
                }

                if (canManageRoleTemplates)
                {
                    adminItems.Add(CreateItem(url, tenantToken, "Role Templates", "Admin", "RoleManagement", "fas fa-user-tag",
                        IsAdminActionActive(currentController, currentAction, "RoleManagement")));
                    adminItems.Add(CreateItem(url, tenantToken, "Email Templates", "Admin", "EmailTemplates", "fas fa-envelope-open-text",
                        IsAdminActionActive(currentController, currentAction, "EmailTemplates")));
                }

                if (!isSuperAdminUser || isImpersonating)
                {
                    if (!isAdminUser || canViewDepartments || isImpersonating)
                    {
                        adminItems.Add(CreateItem(url, tenantToken, "Departments", "Departments", "Index", "fas fa-building",
                            IsControllerActive(currentController, "Departments")));
                    }
                }

                if (isAdminUser || isImpersonating || isSuperAdminUser)
                {
                    adminItems.Add(CreateItem(url, tenantToken, "HR CC Emails", "Admin", "HrCcEmails", "fas fa-at",
                        IsAdminActionActive(currentController, currentAction, "HrCcEmails")));
                }
            }

            AddGroup(model, "admin", "Admin", "navAdminMenu", adminItems);

            var insightsItems = new List<NavMenuItemModel>();
            if (isAdminUser || isImpersonating)
            {
                if (canViewReports || isImpersonating)
                {
                    insightsItems.Add(CreateItem(url, tenantToken, "Reports", "ReportGenerator", "Index", "fas fa-chart-bar",
                        IsReportsActive(currentController, currentAction, currentModuleKey)));
                }

                if (canViewSecurityLogs || isImpersonating)
                {
                    insightsItems.Add(CreateItem(url, tenantToken, "Security Logs", "Admin", "SecurityLogs", "fas fa-shield-alt",
                        IsAdminActionActive(currentController, currentAction, "SecurityLogs")));
                }
            }

            AddGroup(model, "insights", "Insights", "navInsightsMenu", insightsItems);
        }

        private static NavMenuGroupModel BuildSystemGroup(
            MvcUrlHelper url,
            string tenantToken,
            string currentController,
            string currentAction)
        {
            var items = new List<NavMenuItemModel>
            {
                CreateItem(url, tenantToken, "Companies", "Companies", "Index", "fas fa-building",
                    IsControllerActive(currentController, "Companies")),
                CreateItem(url, tenantToken, "Licenses", "Licenses", "Index", "fas fa-key",
                    IsControllerActive(currentController, "Licenses")),
                CreateItem(url, tenantToken, "System Positions", "Positions", "Index", "fas fa-sitemap",
                    IsControllerActive(currentController, "Positions")),
                CreateItem(url, tenantToken, "System Users", "Admin", "GlobalUserManagement", "fas fa-users-cog",
                    IsAdminActionActive(currentController, currentAction, "GlobalUserManagement")),
                CreateItem(url, tenantToken, "Role Templates", "Admin", "RoleManagement", "fas fa-user-tag",
                    IsAdminActionActive(currentController, currentAction, "RoleManagement"))
            };

            bool isActive = items.Any(i => i.IsActive) ||
                            (string.Equals(currentController, "Admin", StringComparison.OrdinalIgnoreCase) &&
                             (string.Equals(currentAction, "GlobalUserManagement", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(currentAction, "RoleManagement", StringComparison.OrdinalIgnoreCase)));

            return new NavMenuGroupModel
            {
                Key = "system",
                Label = "System Administration",
                DropdownId = "saSystemMenu",
                Items = items,
                IsActive = isActive
            };
        }

        private static void AddGroup(NavMenuModel model, string key, string label, string dropdownId, IList<NavMenuItemModel> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            model.Groups.Add(new NavMenuGroupModel
            {
                Key = key,
                Label = label,
                DropdownId = dropdownId,
                Items = items,
                IsActive = items.Any(i => i.IsActive)
            });
        }

        private static NavMenuItemModel CreateItem(
            MvcUrlHelper url,
            string tenantToken,
            string label,
            string controller,
            string action,
            string iconClass,
            bool isActive)
        {
            return new NavMenuItemModel
            {
                Label = label,
                Url = url.Action(action, controller, new { tenant = tenantToken }),
                IconClass = iconClass,
                IsActive = isActive
            };
        }

        private static bool IsControllerActive(string currentController, string controller)
        {
            return string.Equals(currentController, controller, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAdminActionActive(string currentController, string currentAction, string action)
        {
            return string.Equals(currentController, "Admin", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(currentAction, action, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAdminUserManagementActive(string currentController, string currentAction)
        {
            return string.Equals(currentController, "Admin", StringComparison.OrdinalIgnoreCase) &&
                   (string.Equals(currentAction, "UserManagement", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentAction, "GlobalUserManagement", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentAction, "CreateUser", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentAction, "UpdateUserRole", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsQuestionsModuleActive(string currentController, string currentAction, string currentModuleKey)
        {
            if (string.Equals(currentModuleKey, RoleModuleCatalog.Questions, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(currentController, "Questionnaire", StringComparison.OrdinalIgnoreCase) ||
                   IsAdminActionActive(currentController, currentAction, "Questions") ||
                   IsAdminActionActive(currentController, currentAction, "QuestionnaireTemplates") ||
                   IsAdminActionActive(currentController, currentAction, "EditQuestionnaireTemplate");
        }

        private static bool IsReportsActive(string currentController, string currentAction, string currentModuleKey)
        {
            if (string.Equals(currentModuleKey, RoleModuleCatalog.Reports, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsControllerActive(currentController, "ReportGenerator") ||
                   IsControllerActive(currentController, "Reports");
        }

        private static string FormatDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "User";
            }

            if (name.Length == 1)
            {
                return name.ToUpperInvariant();
            }

            return char.ToUpper(name[0]) + name.Substring(1).ToLower();
        }

        private static string GetInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            var parts = displayName.Split(new[] { ' ', '.', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return string.Concat(char.ToUpper(parts[0][0]), char.ToUpper(parts[1][0]));
            }

            return displayName.Substring(0, Math.Min(2, displayName.Length)).ToUpperInvariant();
        }

        private static string GetRoleBadgeLabel(bool isSuperAdminUser, bool isAdminUser, bool isClientUser)
        {
            if (isSuperAdminUser)
            {
                return "SuperAdmin";
            }

            if (isAdminUser)
            {
                return "Admin";
            }

            if (isClientUser)
            {
                return "Applicant";
            }

            return "User";
        }

        private static string GetRoleBadgeClass(bool isSuperAdminUser, bool isAdminUser, bool isClientUser)
        {
            if (isSuperAdminUser)
            {
                return "app-nav-role--super";
            }

            if (isAdminUser)
            {
                return "app-nav-role--admin";
            }

            if (isClientUser)
            {
                return "app-nav-role--applicant";
            }

            return "app-nav-role--default";
        }
    }
}
