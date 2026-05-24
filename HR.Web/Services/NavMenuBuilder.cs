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
        public Uri Url { get; set; }
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
        public Uri Url { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public Uri LogoUrl { get; set; }
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
        public Uri StopImpersonatingUrl { get; set; }
        public string TenantToken { get; set; }
        public bool IsAuthenticated { get; set; }
        public Uri LoginUrl { get; set; }
        public Uri RegisterUrl { get; set; }
        public Uri ProfileUrl { get; set; }
        public Uri ChangePasswordUrl { get; set; }
        public Uri LogoutUrl { get; set; }
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
            var session = httpContext?.Session;
            var identity = user?.Identity;
            bool isAuthenticated = identity != null && identity.IsAuthenticated;
            bool isSuperAdminUser = isAuthenticated && user.IsInRole("SuperAdmin");
            bool isAdminUser = isAuthenticated && user.IsInRole("Admin");
            bool isClientUser = isAuthenticated && !isSuperAdminUser && !isAdminUser;
            bool isImpersonating = session != null && session["ImpersonatedCompanyId"] != null;

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
                LoginUrl = ParseRelativePath(url.Action("Login", "Account", new { tenant = tenantToken })),
                RegisterUrl = ParseRelativePath(url.Action("Register", "Account", new { tenant = tenantToken })),
                ProfileUrl = ParseRelativePath(url.Action("Profile", "Account", new { tenant = tenantToken })),
                ChangePasswordUrl = ParseRelativePath(url.Action("ChangePassword", "Account", new { tenant = tenantToken })),
                LogoutUrl = ParseRelativePath(url.Action("Logout", "Account", new { tenant = tenantToken })),
                ShowImpersonationChip = isImpersonating,
                ImpersonatedCompanyName = isImpersonating
                    ? (session["ImpersonatedCompanyName"] as string ?? tenantContext?.Name ?? "Tenant")
                    : null,
                StopImpersonatingUrl = ParseRelativePath(url.Action("StopImpersonating", "Companies")),
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
            string brandUrlPath = url.Action("Index", "Positions", new { tenant = tenantToken });
            if (isAuthenticated)
            {
                if (isImpersonating)
                {
                    brandUrlPath = url.Action("Index", "Dashboard", new { tenant = tenantToken });
                }
                else if (isSuperAdminUser)
                {
                    brandUrlPath = url.Action("Index", "Companies", new { tenant = tenantToken });
                }
                else if (isAdminUser)
                {
                    brandUrlPath = url.Action("Index", "Dashboard", new { tenant = tenantToken });
                }
            }

            if (tenantContext != null)
            {
                var logoUrl = ResolveCompanyLogoUri(tenantContext.LogoPath, url);
                var hasName = !string.IsNullOrWhiteSpace(tenantContext.Name);
                var hasLogo = logoUrl != null;

                return new NavBrandModel
                {
                    Url = ParseRelativePath(brandUrlPath),
                    Title = hasName ? tenantContext.Name.Trim() : HR.Web.Helpers.AppConfig.ProductName,
                    Subtitle = hasLogo && hasName ? null : HR.Web.Helpers.AppConfig.ProductName,
                    LogoUrl = logoUrl,
                    IsTenantBranded = true
                };
            }

            return new NavBrandModel
            {
                Url = ParseRelativePath(brandUrlPath),
                Title = HR.Web.Helpers.AppConfig.ProductName,
                Subtitle = HR.Web.Helpers.AppConfig.PublisherName,
                LogoUrl = ParseRelativePath(url.Content("~/Content/images/nanosoft-logo-transparent.png")),
                IsTenantBranded = false
            };
        }

        public static Uri ResolveCompanyLogoUri(string logoPath, MvcUrlHelper url)
        {
            if (string.IsNullOrWhiteSpace(logoPath) || url == null)
            {
                return null;
            }

            var path = logoPath.Trim().Replace('\\', '/');
            if (path.StartsWith("~/", StringComparison.Ordinal))
            {
                return ParseRelativePath(url.Content(path));
            }

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                return ParseRelativePath(url.Content("~" + path));
            }

            return ParseRelativePath(url.Content("~/" + path.TrimStart('/')));
        }

        private static Uri ParseRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return Uri.TryCreate(path, UriKind.Relative, out var parsedUri) ? parsedUri : null;
        }

        private static NavUserIdentityModel BuildUserIdentity(
            System.Security.Principal.IPrincipal user,
            bool isAuthenticated,
            bool isSuperAdminUser,
            bool isAdminUser,
            bool isClientUser)
        {
            if (!isAuthenticated || user?.Identity == null)
            {
                return new NavUserIdentityModel { ShowAccountMenu = false };
            }

            var identityName = user.Identity.Name;
            var displayName = FormatDisplayName(identityName);
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
                Url = ParseRelativePath(url.Action(homeAction, homeController, new { tenant = tenantToken })),
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
                    Url = ParseRelativePath(url.Action("Index", "Positions", new { tenant = tenantToken })),
                    IconClass = "fas fa-sitemap",
                    IsActive = string.Equals(currentController, "Positions", StringComparison.OrdinalIgnoreCase)
                });
            }

            if (canViewApplications)
            {
                flat.Add(new NavMenuItemModel
                {
                    Label = "Applications",
                    Url = ParseRelativePath(url.Action("Index", "Applications", new { tenant = tenantToken })),
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
            var context = new NavMenuBuildContext
            {
                Model = model,
                Url = url,
                TenantToken = tenantToken,
                CurrentController = currentController,
                CurrentAction = currentAction,
                CurrentModuleKey = currentModuleKey,
                IsSuperAdminUser = isSuperAdminUser,
                IsAdminUser = isAdminUser,
                IsImpersonating = isImpersonating,
                CanViewPositions = canViewPositions,
                CanViewApplications = canViewApplications,
                CanViewInterviews = canViewInterviews,
                CanViewQuestions = canViewQuestions,
                CanViewApplicants = canViewApplicants,
                CanViewUsers = canViewUsers,
                CanViewSecurityLogs = canViewSecurityLogs,
                CanViewReports = canViewReports,
                CanViewDepartments = canViewDepartments,
                CanManageRoleTemplates = canManageRoleTemplates
            };

            AddGroup(model, "hire", "Hire", "navHireMenu", BuildHireMenuItems(context));
            AddGroup(model, "assess", "Assess", "navAssessMenu", BuildAssessMenuItems(context));
            AddGroup(model, "admin", "Admin", "navAdminMenu", BuildAdminMenuItems(context));
            AddGroup(model, "insights", "Insights", "navInsightsMenu", BuildInsightsMenuItems(context));
        }

        private sealed class NavMenuBuildContext
        {
            public NavMenuModel Model { get; set; }
            public MvcUrlHelper Url { get; set; }
            public string TenantToken { get; set; }
            public string CurrentController { get; set; }
            public string CurrentAction { get; set; }
            public string CurrentModuleKey { get; set; }
            public bool IsSuperAdminUser { get; set; }
            public bool IsAdminUser { get; set; }
            public bool IsImpersonating { get; set; }
            public bool CanViewPositions { get; set; }
            public bool CanViewApplications { get; set; }
            public bool CanViewInterviews { get; set; }
            public bool CanViewQuestions { get; set; }
            public bool CanViewApplicants { get; set; }
            public bool CanViewUsers { get; set; }
            public bool CanViewSecurityLogs { get; set; }
            public bool CanViewReports { get; set; }
            public bool CanViewDepartments { get; set; }
            public bool CanManageRoleTemplates { get; set; }
        }

        private static List<NavMenuItemModel> BuildHireMenuItems(NavMenuBuildContext context)
        {
            var hireItems = new List<NavMenuItemModel>();
            if (!(context.IsSuperAdminUser && !context.IsImpersonating) &&
                (!context.IsAdminUser || context.CanViewPositions || context.IsImpersonating))
            {
                hireItems.Add(CreateItem(context.Url, context.TenantToken, "Positions", "Positions", "Index", "fas fa-sitemap",
                    IsControllerActive(context.CurrentController, "Positions")));
            }

            if (!context.IsSuperAdminUser || context.IsImpersonating)
            {
                if (!context.IsAdminUser || context.CanViewApplications || context.IsImpersonating)
                {
                    hireItems.Add(CreateItem(context.Url, context.TenantToken, "Applications", "Applications", "Index", "fas fa-file-alt",
                        IsControllerActive(context.CurrentController, "Applications")));
                }

                if (!context.IsAdminUser || context.CanViewInterviews || context.IsImpersonating)
                {
                    hireItems.Add(CreateItem(context.Url, context.TenantToken, "Interviews", "Interviews", "Index", "fas fa-comments",
                        IsControllerActive(context.CurrentController, "Interviews")));
                }
            }

            if (context.IsAdminUser || context.IsImpersonating)
            {
                if (context.CanViewApplicants || context.IsImpersonating)
                {
                    hireItems.Add(CreateItem(context.Url, context.TenantToken, "Applicants", "Applicants", "Index", "fas fa-users",
                        IsControllerActive(context.CurrentController, "Applicants")));
                }
            }

            return hireItems;
        }

        private static List<NavMenuItemModel> BuildAssessMenuItems(NavMenuBuildContext context)
        {
            var assessItems = new List<NavMenuItemModel>();
            if (context.IsAdminUser || context.IsImpersonating)
            {
                if (context.CanViewQuestions || context.IsImpersonating)
                {
                    assessItems.Add(CreateItem(context.Url, context.TenantToken, "Questions", "Admin", "Questions", "fas fa-question-circle",
                        IsQuestionsModuleActive(context.CurrentController, context.CurrentAction, context.CurrentModuleKey)));
                }
            }

            return assessItems;
        }

        private static List<NavMenuItemModel> BuildAdminMenuItems(NavMenuBuildContext context)
        {
            var adminItems = new List<NavMenuItemModel>();
            if (!(context.IsAdminUser || context.IsImpersonating))
            {
                return adminItems;
            }

            if (context.CanViewUsers || context.IsImpersonating)
            {
                adminItems.Add(CreateItem(context.Url, context.TenantToken, "User Management", "Admin", "UserManagement", "fas fa-user-shield",
                    IsAdminUserManagementActive(context.CurrentController, context.CurrentAction)));
            }

            if (context.CanManageRoleTemplates)
            {
                adminItems.Add(CreateItem(context.Url, context.TenantToken, "Role Templates", "Admin", "RoleManagement", "fas fa-user-tag",
                    IsAdminActionActive(context.CurrentController, context.CurrentAction, "RoleManagement")));
                adminItems.Add(CreateItem(context.Url, context.TenantToken, "Email Templates", "Admin", "EmailTemplates", "fas fa-envelope-open-text",
                    IsAdminActionActive(context.CurrentController, context.CurrentAction, "EmailTemplates")));
            }

            if (!context.IsSuperAdminUser || context.IsImpersonating)
            {
                if (!context.IsAdminUser || context.CanViewDepartments || context.IsImpersonating)
                {
                    adminItems.Add(CreateItem(context.Url, context.TenantToken, "Departments", "Departments", "Index", "fas fa-building",
                        IsControllerActive(context.CurrentController, "Departments")));
                }
            }

            if (context.IsAdminUser || context.IsImpersonating || context.IsSuperAdminUser)
            {
                adminItems.Add(CreateItem(context.Url, context.TenantToken, "HR CC Emails", "Admin", "HrCcEmails", "fas fa-at",
                    IsAdminActionActive(context.CurrentController, context.CurrentAction, "HrCcEmails")));
            }

            return adminItems;
        }

        private static List<NavMenuItemModel> BuildInsightsMenuItems(NavMenuBuildContext context)
        {
            var insightsItems = new List<NavMenuItemModel>();
            if (!(context.IsAdminUser || context.IsImpersonating))
            {
                return insightsItems;
            }

            if (context.CanViewReports || context.IsImpersonating)
            {
                insightsItems.Add(CreateItem(context.Url, context.TenantToken, "Reports", "ReportGenerator", "Index", "fas fa-chart-bar",
                    IsReportsActive(context.CurrentController, context.CurrentAction, context.CurrentModuleKey)));
            }

            if (context.CanViewSecurityLogs || context.IsImpersonating)
            {
                insightsItems.Add(CreateItem(context.Url, context.TenantToken, "Security Logs", "Admin", "SecurityLogs", "fas fa-shield-alt",
                    IsAdminActionActive(context.CurrentController, context.CurrentAction, "SecurityLogs")));
            }

            return insightsItems;
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
            if (model == null || items == null || items.Count == 0)
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
                Url = ParseRelativePath(url.Action(action, controller, new { tenant = tenantToken })),
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
