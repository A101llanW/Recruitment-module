using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        private sealed class RoleSelectionResolution
        {
            public bool IsValid { get; set; }
            public string BaseRole { get; set; }
            public int? RoleDefinitionId { get; set; }
            public string DisplayRoleName { get; set; }
            public string ErrorMessage { get; set; }
        }

        private sealed class RoleManagementScopeContext
        {
            public bool IsGlobalSuperAdmin { get; set; }
            public bool IsImpersonating { get; set; }
            public int? CompanyId { get; set; }
        }

        private RoleManagementScopeContext GetRoleManagementScopeContext()
        {
            return new RoleManagementScopeContext
            {
                IsGlobalSuperAdmin = _tenantService.IsSuperAdmin(),
                IsImpersonating = _tenantService.IsImpersonating(),
                CompanyId = _tenantService.GetCurrentUserCompanyId()
            };
        }

        private static IQueryable<RoleDefinition> ApplyRoleDefinitionScopeFilter(
            IQueryable<RoleDefinition> query,
            RoleManagementScopeContext scope,
            int? catalogCompanyId = null)
        {
            if (scope.IsGlobalSuperAdmin)
            {
                if (catalogCompanyId.HasValue)
                {
                    var companyId = catalogCompanyId.Value;
                    return query.Where(r => !r.CompanyId.HasValue || r.CompanyId == companyId);
                }

                return query;
            }

            if (scope.IsImpersonating)
            {
                return query.Where(r => r.CompanyId == scope.CompanyId);
            }

            return query.Where(r => !r.CompanyId.HasValue || r.CompanyId == scope.CompanyId);
        }

        private static bool CanManageRoleDefinition(RoleDefinition role, RoleManagementScopeContext scope)
        {
            if (role == null)
            {
                return false;
            }

            if (scope.IsGlobalSuperAdmin)
            {
                return true;
            }

            return role.CompanyId.HasValue &&
                   scope.CompanyId.HasValue &&
                   role.CompanyId.Value == scope.CompanyId.Value;
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult RoleManagement(int? editRoleId)
        {
            if (!_rolePermissionService.CanCurrentUserManageRoleDefinitions())
            {
                return new HttpStatusCodeResult(403, "Access Denied: Only full company admins and superadmins can manage role templates.");
            }

            var scope = GetRoleManagementScopeContext();
            var model = new RoleManagementPageViewModel
            {
                CompanyId = scope.IsGlobalSuperAdmin ? (int?)null : scope.CompanyId
            };

            if (editRoleId.HasValue)
            {
                var role = _uow.Context.RoleDefinitions
                    .Include(r => r.RolePermissions)
                    .FirstOrDefault(r => r.Id == editRoleId.Value && r.IsActive);
                if (role == null)
                {
                    return HttpNotFound();
                }

                if (!CanManageRoleDefinition(role, scope))
                {
                    return new HttpStatusCodeResult(403, "Access Denied");
                }

                model = PopulateRoleEditModel(role, model, scope);
            }

            return View(BuildRoleManagementPageViewModel(model));
        }

        private static IEnumerable<RolePermissionInputViewModel> GetSelectedModulePermissions(IEnumerable<RolePermissionInputViewModel> modulePermissions)
        {
            return (modulePermissions ?? Enumerable.Empty<RolePermissionInputViewModel>())
                .Where(p => !string.Equals(p.AccessLevel, RoleAccessLevels.None, StringComparison.OrdinalIgnoreCase));
        }

        private void PersistRolePermissions(int roleDefinitionId, IEnumerable<RolePermissionInputViewModel> modulePermissions)
        {
            foreach (var permission in GetSelectedModulePermissions(modulePermissions))
            {
                _uow.RolePermissions.Add(new RolePermission
                {
                    RoleDefinitionId = roleDefinitionId,
                    ModuleKey = permission.ModuleKey,
                    AccessLevel = permission.AccessLevel
                });
            }

            _uow.Complete();
        }

        private void ReplaceRolePermissions(RoleDefinition role, IEnumerable<RolePermissionInputViewModel> modulePermissions)
        {
            var existingPermissions = (role.RolePermissions ?? Enumerable.Empty<RolePermission>()).ToList();
            _uow.Context.Set<RolePermission>().RemoveRange(existingPermissions);
            _uow.Complete();
            PersistRolePermissions(role.Id, modulePermissions);
        }

        private static RoleDefinition BuildNewRoleDefinition(RoleManagementPageViewModel model, int? scopedCompanyId, string createdBy)
        {
            return new RoleDefinition
            {
                CompanyId = scopedCompanyId,
                Name = model.Name != null ? model.Name.Trim() : null,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                CreatedByUserName = createdBy,
                CreatedDate = DateTime.Now,
                IsActive = true
            };
        }

        private static void ApplyRoleDefinitionFields(RoleDefinition role, RoleManagementPageViewModel model, int? scopedCompanyId)
        {
            role.CompanyId = scopedCompanyId;
            role.Name = model.Name != null ? model.Name.Trim() : null;
            role.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        }

        private ActionResult RoleManagementForm(RoleManagementPageViewModel model, int? scopedCompanyId, int? editingRoleId = null)
        {
            if (model != null)
            {
                model.CompanyId = scopedCompanyId;
                if (editingRoleId.HasValue)
                {
                    model.EditingRoleId = editingRoleId;
                }
            }

            return View("RoleManagement", BuildRoleManagementPageViewModel(model ?? new RoleManagementPageViewModel()));
        }

        private void LogRoleTemplateCreated(RoleDefinition roleDefinition, int? scopedCompanyId)
        {
            var scopeCompany = scopedCompanyId.HasValue ? _uow.Companies.Get(scopedCompanyId.Value) : null;
            var scopeName = scopeCompany != null ? scopeCompany.Name : "Global";
            _auditService.LogAction(
                GetAuditActorName(),
                "ROLE_TEMPLATE_CREATED",
                "RoleManagement",
                roleDefinition.Id.ToString(),
                true,
                string.Format("Created role template '{0}' for scope '{1}'", roleDefinition.Name, scopeName));
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult CreateRole(RoleManagementPageViewModel model)
        {
            if (!_rolePermissionService.CanCurrentUserManageRoleDefinitions())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            if (model == null)
            {
                return View("RoleManagement", BuildRoleManagementPageViewModel(new RoleManagementPageViewModel()));
            }

            var scope = GetRoleManagementScopeContext();
            var actorCompanyId = scope.CompanyId;
            var scopedCompanyId = scope.IsGlobalSuperAdmin ? model.CompanyId : actorCompanyId;

            NormalizeRolePermissionSelections(model);
            ValidateRoleDefinitionModel(model, scopedCompanyId, null);

            if (!ModelState.IsValid)
            {
                return RoleManagementForm(model, scopedCompanyId);
            }

            var roleDefinition = BuildNewRoleDefinition(model, scopedCompanyId, GetAuditActorName());
            _uow.RoleDefinitions.Add(roleDefinition);
            _uow.Complete();
            PersistRolePermissions(roleDefinition.Id, model.ModulePermissions);
            LogRoleTemplateCreated(roleDefinition, scopedCompanyId);

            TempData["SuccessMessage"] = string.Format("Role template '{0}' created successfully.", roleDefinition.Name);
            return RedirectToAction("RoleManagement");
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateRole(RoleManagementPageViewModel model)
        {
            if (!_rolePermissionService.CanCurrentUserManageRoleDefinitions())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            if (model == null)
            {
                return HttpNotFound();
            }

            if (!model.EditingRoleId.HasValue || model.EditingRoleId.Value <= 0)
            {
                return HttpNotFound();
            }

            var roleId = model.EditingRoleId.Value;
            var scope = GetRoleManagementScopeContext();
            var actorCompanyId = scope.CompanyId;
            var role = _uow.Context.RoleDefinitions
                .Include(r => r.RolePermissions)
                .FirstOrDefault(r => r.Id == roleId && r.IsActive);

            if (role == null)
            {
                return HttpNotFound();
            }

            if (!CanManageRoleDefinition(role, scope))
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var scopedCompanyId = scope.IsGlobalSuperAdmin ? model.CompanyId : actorCompanyId;
            NormalizeRolePermissionSelections(model);
            ValidateRoleDefinitionModel(model, scopedCompanyId, roleId);

            if (!ModelState.IsValid)
            {
                return RoleManagementForm(model, scopedCompanyId, roleId);
            }

            ApplyRoleDefinitionFields(role, model, scopedCompanyId);
            ReplaceRolePermissions(role, model.ModulePermissions);
            _auditService.LogAction(
                GetAuditActorName(),
                "ROLE_TEMPLATE_UPDATED",
                "RoleManagement",
                role.Id.ToString(),
                true,
                string.Format("Updated role template '{0}'", role.Name));

            TempData["SuccessMessage"] = string.Format("Role template '{0}' updated successfully.", role.Name);
            return RedirectToAction("RoleManagement");
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteRole(int id)
        {
            if (!_rolePermissionService.CanCurrentUserManageRoleDefinitions())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var scope = GetRoleManagementScopeContext();
            var actorCompanyId = scope.CompanyId;
            var role = _uow.Context.RoleDefinitions
                .Include(r => r.Company)
                .Include(r => r.RolePermissions)
                .Include(r => r.Users)
                .FirstOrDefault(r => r.Id == id);

            if (role == null)
            {
                return HttpNotFound();
            }

            if (!CanManageRoleDefinition(role, scope))
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            if (role.Users != null && role.Users.Any())
            {
                TempData["ErrorMessage"] = string.Format("Role template '{0}' is currently assigned to one or more users and cannot be deleted.", role.Name);
                return RedirectToAction("RoleManagement");
            }

            var roleName = role.Name;
            var permissions = (role.RolePermissions ?? Enumerable.Empty<RolePermission>()).ToList();
            _uow.Context.Set<RolePermission>().RemoveRange(permissions);
            _uow.RoleDefinitions.Remove(role);
            _uow.Complete();

            _auditService.LogAction(
                GetAuditActorName(),
                "ROLE_TEMPLATE_DELETED",
                "RoleManagement",
                id.ToString(),
                true,
                string.Format("Deleted role template '{0}'", roleName));

            TempData["SuccessMessage"] = string.Format("Role template '{0}' deleted successfully.", roleName);
            return RedirectToAction("RoleManagement");
        }

        private RoleManagementPageViewModel BuildRoleManagementPageViewModel(RoleManagementPageViewModel model)
        {
            model = model ?? new RoleManagementPageViewModel();
            var scope = GetRoleManagementScopeContext();

            model.IsActualSuperAdmin = scope.IsGlobalSuperAdmin;
            model.CurrentCompanyId = scope.CompanyId;
            model.Companies = scope.IsGlobalSuperAdmin
                ? _uow.Companies.GetAll().OrderBy(c => c.Name).ToList()
                : new List<Company>();
            model.CompanyId = scope.IsGlobalSuperAdmin ? model.CompanyId : scope.CompanyId;
            model.ModulePermissions = BuildModulePermissionInputs(model.ModulePermissions);
            model.ExistingRoles = BuildRoleDefinitionSummaries(scope);

            return model;
        }

        private List<RolePermissionInputViewModel> BuildModulePermissionInputs(IEnumerable<RolePermissionInputViewModel> existingInputs)
        {
            var existingByModule = (existingInputs ?? Enumerable.Empty<RolePermissionInputViewModel>())
                .ToDictionary(p => p.ModuleKey, p => p, StringComparer.OrdinalIgnoreCase);
            var modules = new List<RolePermissionInputViewModel>();

            foreach (var module in RoleModuleCatalog.All)
            {
                existingByModule.TryGetValue(module.Key, out var existing);
                modules.Add(BuildModulePermissionInput(module, existing));
            }

            return modules;
        }

        private List<RoleDefinitionSummaryViewModel> BuildRoleDefinitionSummaries(RoleManagementScopeContext scope)
        {
            var query = _uow.Context.RoleDefinitions
                .Include(r => r.Company)
                .Include(r => r.RolePermissions)
                .Include(r => r.Users)
                .Where(r => r.IsActive)
                .AsQueryable();

            query = ApplyRoleDefinitionScopeFilter(query, scope);

            var roles = query
                .OrderBy(r => r.CompanyId.HasValue)
                .ThenBy(r => r.Company != null ? r.Company.Name : "Global")
                .ThenBy(r => r.Name)
                .ToList();

            return roles.Select(role => new RoleDefinitionSummaryViewModel
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                ScopeName = BuildScopeName(role.Company),
                IsGlobal = !role.CompanyId.HasValue,
                CreatedDate = role.CreatedDate,
                CreatedByUserName = role.CreatedByUserName,
                CanDelete = CanManageRoleDefinition(role, scope),
                AssignedUsersCount = role.Users != null ? role.Users.Count : 0,
                Permissions = (role.RolePermissions ?? Enumerable.Empty<RolePermission>())
                    .OrderBy(p => p.ModuleKey)
                    .Select(p =>
                    {
                        var definition = RoleModuleCatalog.Find(p.ModuleKey);
                        return new RolePermissionInputViewModel
                        {
                            ModuleKey = p.ModuleKey,
                            DisplayName = definition != null ? definition.DisplayName : p.ModuleKey,
                            Description = definition != null ? definition.Description : string.Empty,
                            IconClass = definition != null ? definition.IconClass : "fa-lock",
                            IsSelected = !string.Equals(p.AccessLevel, RoleAccessLevels.None, StringComparison.OrdinalIgnoreCase),
                            AccessLevel = p.AccessLevel
                        };
                    })
                    .ToList()
            }).ToList();
        }

        private static string BuildScopeName(Company company)
        {
            return company != null ? company.Name : "Global";
        }

        private RoleManagementPageViewModel PopulateRoleEditModel(RoleDefinition role, RoleManagementPageViewModel model, RoleManagementScopeContext scope)
        {
            model.EditingRoleId = role.Id;
            model.Name = role.Name;
            model.Description = role.Description;
            model.CompanyId = scope.IsGlobalSuperAdmin ? role.CompanyId : scope.CompanyId;

            model.ModulePermissions = RoleModuleCatalog.All
                .Select(module =>
                {
                    var rolePermissions = role.RolePermissions ?? Enumerable.Empty<RolePermission>();
                    var existingPermission = rolePermissions.FirstOrDefault(p => string.Equals(p.ModuleKey, module.Key, StringComparison.OrdinalIgnoreCase));
                    var accessLevel = existingPermission != null ? existingPermission.AccessLevel : RoleAccessLevels.None;
                    return new RolePermissionInputViewModel
                    {
                        ModuleKey = module.Key,
                        DisplayName = module.DisplayName,
                        Description = module.Description,
                        IconClass = module.IconClass,
                        IsSelected = string.Equals(accessLevel, RoleAccessLevels.Manage, StringComparison.OrdinalIgnoreCase),
                        IsReadOnlySelected = string.Equals(accessLevel, RoleAccessLevels.View, StringComparison.OrdinalIgnoreCase),
                        AccessLevel = accessLevel
                    };
                })
                .ToList();

            return model;
        }

        private void NormalizeRolePermissionSelections(RoleManagementPageViewModel model)
        {
            if (model == null)
            {
                return;
            }

            model.ModulePermissions = BuildModulePermissionInputs(model.ModulePermissions);

            foreach (var permission in model.ModulePermissions)
            {
                permission.AccessLevel = ResolveAccessLevel(permission.IsSelected, permission.IsReadOnlySelected);

                if (!RoleAccessLevels.OrderedValues.Contains(permission.AccessLevel))
                {
                    permission.AccessLevel = RoleAccessLevels.None;
                }
            }
        }

        private static string ResolveAccessLevel(bool allowManage, bool allowReadOnly)
        {
            if (allowManage)
            {
                return RoleAccessLevels.Manage;
            }

            if (allowReadOnly)
            {
                return RoleAccessLevels.View;
            }

            return RoleAccessLevels.None;
        }

        private static RolePermissionInputViewModel BuildModulePermissionInput(RoleModuleDefinition module, RolePermissionInputViewModel existing)
        {
            var accessLevel = string.IsNullOrWhiteSpace(existing?.AccessLevel)
                ? RoleAccessLevels.None
                : existing.AccessLevel;
            var allowManage = existing != null && existing.IsSelected;
            var allowReadOnly = existing != null && existing.IsReadOnlySelected;

            if (allowManage || allowReadOnly)
            {
                accessLevel = ResolveAccessLevel(allowManage, allowReadOnly);
            }
            else if (!string.Equals(accessLevel, RoleAccessLevels.None, StringComparison.OrdinalIgnoreCase))
            {
                allowManage = string.Equals(accessLevel, RoleAccessLevels.Manage, StringComparison.OrdinalIgnoreCase);
                allowReadOnly = string.Equals(accessLevel, RoleAccessLevels.View, StringComparison.OrdinalIgnoreCase);
            }

            return new RolePermissionInputViewModel
            {
                ModuleKey = module.Key,
                DisplayName = module.DisplayName,
                Description = module.Description,
                IconClass = module.IconClass,
                IsSelected = allowManage,
                IsReadOnlySelected = allowReadOnly,
                AccessLevel = accessLevel
            };
        }

        private void ValidateRoleDefinitionModel(RoleManagementPageViewModel model, int? scopedCompanyId, int? editingRoleId)
        {
            if (model == null)
            {
                ModelState.AddModelError("", "Role template data is required.");
                return;
            }

            var scope = GetRoleManagementScopeContext();
            ValidateRoleDefinitionCompanyScope(scopedCompanyId, scope);
            ValidateRoleDefinitionModuleSelection(model);
            ValidateRoleDefinitionNameUniqueness(model, scopedCompanyId, editingRoleId);
        }

        private static bool HasSelectedModulePermissions(RoleManagementPageViewModel model)
        {
            return model.ModulePermissions != null &&
                   model.ModulePermissions.Any(p => !string.Equals(p.AccessLevel, RoleAccessLevels.None, StringComparison.OrdinalIgnoreCase));
        }

        private void ValidateRoleDefinitionCompanyScope(int? scopedCompanyId, RoleManagementScopeContext scope)
        {
            if (!scopedCompanyId.HasValue && !scope.IsGlobalSuperAdmin)
            {
                ModelState.AddModelError("CompanyId", "Company context is required to create a role template.");
            }
        }

        private void ValidateRoleDefinitionModuleSelection(RoleManagementPageViewModel model)
        {
            if (!HasSelectedModulePermissions(model))
            {
                ModelState.AddModelError("", "Select at least one module before saving the role template.");
            }
        }

        private void ValidateRoleDefinitionNameUniqueness(RoleManagementPageViewModel model, int? scopedCompanyId, int? editingRoleId)
        {
            var normalizedName = model.Name != null ? model.Name.Trim().ToLower() : string.Empty;
            var duplicateExists = _uow.Context.RoleDefinitions.Any(r =>
                r.IsActive &&
                r.CompanyId == scopedCompanyId &&
                r.Name.ToLower() == normalizedName &&
                (!editingRoleId.HasValue || r.Id != editingRoleId.Value));

            if (duplicateExists)
            {
                ModelState.AddModelError("Name", "A role template with this name already exists in the selected scope.");
            }
        }

        private List<SelectListItem> BuildAvailableRoleOptions(bool isActualSuperAdmin, int? actorCompanyId, int? selectedCompanyId, bool restrictToFullAdmin, string selectedRoleKey)
        {
            var scope = GetRoleManagementScopeContext();
            var options = new List<SelectListItem>();

            if (restrictToFullAdmin && !scope.IsGlobalSuperAdmin)
            {
                options.Add(new SelectListItem
                {
                    Value = "builtin:Admin",
                    Text = "Elevated Control (Admin)",
                    Selected = string.Equals(selectedRoleKey, "builtin:Admin", StringComparison.OrdinalIgnoreCase)
                });
                return options;
            }

            options.Add(new SelectListItem
            {
                Value = "builtin:Client",
                Text = "Standard Access (Client)",
                Selected = string.Equals(selectedRoleKey, "builtin:Client", StringComparison.OrdinalIgnoreCase)
            });
            options.Add(new SelectListItem
            {
                Value = "builtin:Admin",
                Text = "Elevated Control (Admin)",
                Selected = string.Equals(selectedRoleKey, "builtin:Admin", StringComparison.OrdinalIgnoreCase)
            });

            if (scope.IsGlobalSuperAdmin)
            {
                options.Add(new SelectListItem
                {
                    Value = "builtin:SuperAdmin",
                    Text = "Master Authority (SuperAdmin)",
                    Selected = string.Equals(selectedRoleKey, "builtin:SuperAdmin", StringComparison.OrdinalIgnoreCase)
                });
            }

            var roleDefinitions = GetAssignableRoleDefinitions(actorCompanyId, selectedCompanyId);
            foreach (var roleDefinition in roleDefinitions)
            {
                var scopeName = BuildScopeName(roleDefinition.Company);
                options.Add(new SelectListItem
                {
                    Value = "custom:" + roleDefinition.Id,
                    Text = string.Format("Custom Role: {0} ({1})", roleDefinition.Name, scopeName),
                    Selected = string.Equals(selectedRoleKey, "custom:" + roleDefinition.Id, StringComparison.OrdinalIgnoreCase)
                });
            }

            return options;
        }

        private List<RoleDefinition> GetAssignableRoleDefinitions(int? actorCompanyId, int? catalogCompanyId = null)
        {
            var scope = GetRoleManagementScopeContext();
            var query = _uow.Context.RoleDefinitions
                .Include(r => r.Company)
                .Where(r => r.IsActive)
                .AsQueryable();

            query = ApplyRoleDefinitionScopeFilter(query, scope, catalogCompanyId ?? actorCompanyId);

            return query
                .OrderBy(r => r.CompanyId.HasValue)
                .ThenBy(r => r.Company != null ? r.Company.Name : "Global")
                .ThenBy(r => r.Name)
                .ToList();
        }

        private RoleSelectionResolution ResolveRoleSelection(string selectedRoleKey, bool isActualSuperAdmin, int? actorCompanyId, int? targetCompanyId)
        {
            var scope = GetRoleManagementScopeContext();
            if (string.IsNullOrWhiteSpace(selectedRoleKey))
            {
                return RoleSelectionError("Role selection is required.");
            }

            if (selectedRoleKey.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveBuiltinRoleSelection(selectedRoleKey, scope);
            }

            if (!selectedRoleKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            {
                return RoleSelectionError("Unknown role selection.");
            }

            return ResolveCustomRoleSelection(selectedRoleKey, scope, actorCompanyId, targetCompanyId);
        }

        private static RoleSelectionResolution RoleSelectionError(string message)
        {
            return new RoleSelectionResolution { ErrorMessage = message };
        }

        private static RoleSelectionResolution RoleSelectionSuccess(string baseRole, int? roleDefinitionId, string displayName)
        {
            return new RoleSelectionResolution
            {
                IsValid = true,
                BaseRole = baseRole,
                RoleDefinitionId = roleDefinitionId,
                DisplayRoleName = displayName
            };
        }

        private static RoleSelectionResolution ResolveBuiltinRoleSelection(string selectedRoleKey, RoleManagementScopeContext scope)
        {
            var builtinRole = selectedRoleKey.Substring("builtin:".Length);
            if (string.Equals(builtinRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && !scope.IsGlobalSuperAdmin)
            {
                return RoleSelectionError("Only superadmins can assign the SuperAdmin role.");
            }

            return RoleSelectionSuccess(builtinRole, null, builtinRole);
        }

        private RoleSelectionResolution ResolveCustomRoleSelection(string selectedRoleKey, RoleManagementScopeContext scope, int? actorCompanyId, int? targetCompanyId)
        {
            if (!int.TryParse(selectedRoleKey.Substring("custom:".Length), out var roleDefinitionId))
            {
                return RoleSelectionError("Invalid custom role selection.");
            }

            var roleDefinition = _uow.Context.RoleDefinitions
                .Include(r => r.Company)
                .FirstOrDefault(r => r.Id == roleDefinitionId && r.IsActive);

            if (roleDefinition == null)
            {
                return RoleSelectionError("The selected custom role no longer exists.");
            }

            return ValidateCustomRoleAssignment(roleDefinition, scope, actorCompanyId, targetCompanyId);
        }

        private static RoleSelectionResolution ValidateCustomRoleAssignment(
            RoleDefinition roleDefinition,
            RoleManagementScopeContext scope,
            int? actorCompanyId,
            int? targetCompanyId)
        {
            if (!targetCompanyId.HasValue)
            {
                return RoleSelectionError("Custom roles can only be assigned to users that belong to a company.");
            }

            if (roleDefinition.CompanyId.HasValue && roleDefinition.CompanyId.Value != targetCompanyId.Value)
            {
                return RoleSelectionError(string.Format("The custom role '{0}' belongs to a different company scope.", roleDefinition.Name));
            }

            if (scope.IsImpersonating && !roleDefinition.CompanyId.HasValue)
            {
                return RoleSelectionError("Global role templates cannot be assigned while impersonating a company.");
            }

            if (!scope.IsGlobalSuperAdmin && roleDefinition.CompanyId.HasValue && roleDefinition.CompanyId.Value != actorCompanyId)
            {
                return RoleSelectionError("You cannot assign a custom role from another company.");
            }

            return RoleSelectionSuccess("Admin", roleDefinition.Id, roleDefinition.Name);
        }

        private static string BuildRoleSelectionKey(User user)
        {
            if (user != null && user.RoleDefinitionId.HasValue)
            {
                return "custom:" + user.RoleDefinitionId.Value;
            }

            var role = user != null ? user.Role : "Client";
            return "builtin:" + (string.IsNullOrWhiteSpace(role) ? "Client" : role);
        }
    }
}
