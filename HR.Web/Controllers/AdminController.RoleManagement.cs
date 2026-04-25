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

        [Authorize(Roles = "Admin, SuperAdmin")]
        public ActionResult RoleManagement(int? editRoleId)
        {
            if (!_rolePermissionService.CanCurrentUserManageRoleDefinitions())
            {
                return new HttpStatusCodeResult(403, "Access Denied: Only full company admins and superadmins can manage role templates.");
            }

            var isActualSuperAdmin = _tenantService.IsActualSuperAdmin();
            var currentCompanyId = _tenantService.GetCurrentUserCompanyId();
            var model = new RoleManagementPageViewModel
            {
                CompanyId = isActualSuperAdmin ? (int?)null : currentCompanyId
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

                if (!CanEditRoleDefinition(role, isActualSuperAdmin, currentCompanyId))
                {
                    return new HttpStatusCodeResult(403, "Access Denied");
                }

                model = PopulateRoleEditModel(role, model, isActualSuperAdmin, currentCompanyId);
            }

            return View(BuildRoleManagementPageViewModel(model));
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

            var isActualSuperAdmin = _tenantService.IsActualSuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var scopedCompanyId = isActualSuperAdmin ? model.CompanyId : actorCompanyId;

            NormalizeRolePermissionSelections(model, isActualSuperAdmin);
            ValidateRoleDefinitionModel(model, scopedCompanyId, null);

            if (!ModelState.IsValid)
            {
                model.CompanyId = scopedCompanyId;
                return View("RoleManagement", BuildRoleManagementPageViewModel(model));
            }

            var roleDefinition = new RoleDefinition
            {
                CompanyId = scopedCompanyId,
                Name = model.Name != null ? model.Name.Trim() : null,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                CreatedByUserName = User.Identity.Name,
                CreatedDate = DateTime.Now,
                IsActive = true
            };

            _uow.RoleDefinitions.Add(roleDefinition);
            _uow.Complete();

            foreach (var permission in model.ModulePermissions.Where(p => !string.Equals(p.AccessLevel, RoleAccessLevels.None, StringComparison.OrdinalIgnoreCase)))
            {
                _uow.RolePermissions.Add(new RolePermission
                {
                    RoleDefinitionId = roleDefinition.Id,
                    ModuleKey = permission.ModuleKey,
                    AccessLevel = permission.AccessLevel
                });
            }

            _uow.Complete();
            var scopeCompany = scopedCompanyId.HasValue ? _uow.Companies.Get(scopedCompanyId.Value) : null;
            var scopeName = scopeCompany != null ? scopeCompany.Name : "Global";
            _auditService.LogAction(
                User.Identity.Name,
                "ROLE_TEMPLATE_CREATED",
                "RoleManagement",
                roleDefinition.Id.ToString(),
                true,
                string.Format("Created role template '{0}' for scope '{1}'", roleDefinition.Name, scopeName));

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

            if (!model.EditingRoleId.HasValue || model.EditingRoleId.Value <= 0)
            {
                return HttpNotFound();
            }

            var roleId = model.EditingRoleId.Value;
            var isActualSuperAdmin = _tenantService.IsActualSuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var role = _uow.Context.RoleDefinitions
                .Include(r => r.RolePermissions)
                .FirstOrDefault(r => r.Id == roleId && r.IsActive);

            if (role == null)
            {
                return HttpNotFound();
            }

            if (!CanEditRoleDefinition(role, isActualSuperAdmin, actorCompanyId))
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            var scopedCompanyId = isActualSuperAdmin ? model.CompanyId : actorCompanyId;
            NormalizeRolePermissionSelections(model, isActualSuperAdmin);
            ValidateRoleDefinitionModel(model, scopedCompanyId, roleId);

            if (!ModelState.IsValid)
            {
                model.EditingRoleId = roleId;
                model.CompanyId = scopedCompanyId;
                return View("RoleManagement", BuildRoleManagementPageViewModel(model));
            }

            role.CompanyId = scopedCompanyId;
            role.Name = model.Name != null ? model.Name.Trim() : null;
            role.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

            var existingPermissions = role.RolePermissions.ToList();
            _uow.Context.Set<RolePermission>().RemoveRange(existingPermissions);
            _uow.Complete();

            foreach (var permission in model.ModulePermissions.Where(p => !string.Equals(p.AccessLevel, RoleAccessLevels.None, StringComparison.OrdinalIgnoreCase)))
            {
                _uow.RolePermissions.Add(new RolePermission
                {
                    RoleDefinitionId = role.Id,
                    ModuleKey = permission.ModuleKey,
                    AccessLevel = permission.AccessLevel
                });
            }

            _uow.Complete();
            _auditService.LogAction(
                User.Identity.Name,
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

            var isActualSuperAdmin = _tenantService.IsActualSuperAdmin();
            var actorCompanyId = _tenantService.GetCurrentUserCompanyId();
            var role = _uow.Context.RoleDefinitions
                .Include(r => r.Company)
                .Include(r => r.RolePermissions)
                .Include(r => r.Users)
                .FirstOrDefault(r => r.Id == id);

            if (role == null)
            {
                return HttpNotFound();
            }

            if (!CanDeleteRoleDefinition(role, isActualSuperAdmin, actorCompanyId))
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            if (role.Users != null && role.Users.Any())
            {
                TempData["ErrorMessage"] = string.Format("Role template '{0}' is currently assigned to one or more users and cannot be deleted.", role.Name);
                return RedirectToAction("RoleManagement");
            }

            var roleName = role.Name;
            var permissions = role.RolePermissions.ToList();
            _uow.Context.Set<RolePermission>().RemoveRange(permissions);
            _uow.RoleDefinitions.Remove(role);
            _uow.Complete();

            _auditService.LogAction(
                User.Identity.Name,
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
            var isActualSuperAdmin = _tenantService.IsActualSuperAdmin();
            var currentCompanyId = _tenantService.GetCurrentUserCompanyId();

            model.IsActualSuperAdmin = isActualSuperAdmin;
            model.CurrentCompanyId = currentCompanyId;
            model.Companies = isActualSuperAdmin
                ? _uow.Companies.GetAll().OrderBy(c => c.Name).ToList()
                : new List<Company>();
            model.CompanyId = isActualSuperAdmin ? model.CompanyId : currentCompanyId;
            model.ModulePermissions = BuildModulePermissionInputs(model.ModulePermissions);
            model.ExistingRoles = BuildRoleDefinitionSummaries(isActualSuperAdmin, currentCompanyId);

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

        private List<RoleDefinitionSummaryViewModel> BuildRoleDefinitionSummaries(bool isActualSuperAdmin, int? currentCompanyId)
        {
            var query = _uow.Context.RoleDefinitions
                .Include(r => r.Company)
                .Include(r => r.RolePermissions)
                .Include(r => r.Users)
                .Where(r => r.IsActive)
                .AsQueryable();

            if (!isActualSuperAdmin)
            {
                query = query.Where(r => !r.CompanyId.HasValue || r.CompanyId == currentCompanyId);
            }

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
                CanDelete = CanDeleteRoleDefinition(role, isActualSuperAdmin, currentCompanyId),
                AssignedUsersCount = role.Users != null ? role.Users.Count : 0,
                Permissions = role.RolePermissions
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

        private RoleManagementPageViewModel PopulateRoleEditModel(RoleDefinition role, RoleManagementPageViewModel model, bool isActualSuperAdmin, int? currentCompanyId)
        {
            model.EditingRoleId = role.Id;
            model.Name = role.Name;
            model.Description = role.Description;
            model.CompanyId = isActualSuperAdmin ? role.CompanyId : currentCompanyId;

            model.ModulePermissions = RoleModuleCatalog.All
                .Select(module =>
                {
                    var existingPermission = role.RolePermissions.FirstOrDefault(p => string.Equals(p.ModuleKey, module.Key, StringComparison.OrdinalIgnoreCase));
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

        private bool CanEditRoleDefinition(RoleDefinition role, bool isActualSuperAdmin, int? currentCompanyId)
        {
            return CanDeleteRoleDefinition(role, isActualSuperAdmin, currentCompanyId);
        }

        private bool CanDeleteRoleDefinition(RoleDefinition role, bool isActualSuperAdmin, int? currentCompanyId)
        {
            if (role == null)
            {
                return false;
            }

            if (isActualSuperAdmin)
            {
                return true;
            }

            return role.CompanyId.HasValue && currentCompanyId.HasValue && role.CompanyId.Value == currentCompanyId.Value;
        }

        private void NormalizeRolePermissionSelections(RoleManagementPageViewModel model, bool isActualSuperAdmin)
        {
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
            if (!scopedCompanyId.HasValue && !_tenantService.IsActualSuperAdmin())
            {
                ModelState.AddModelError("CompanyId", "Company context is required to create a role template.");
            }

            if (model.ModulePermissions == null || !model.ModulePermissions.Any(p => !string.Equals(p.AccessLevel, RoleAccessLevels.None, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("", "Select at least one module before saving the role template.");
            }

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
            var options = new List<SelectListItem>();

            if (restrictToFullAdmin && !isActualSuperAdmin)
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

            if (isActualSuperAdmin)
            {
                options.Add(new SelectListItem
                {
                    Value = "builtin:SuperAdmin",
                    Text = "Master Authority (SuperAdmin)",
                    Selected = string.Equals(selectedRoleKey, "builtin:SuperAdmin", StringComparison.OrdinalIgnoreCase)
                });
            }

            var roleDefinitions = GetAssignableRoleDefinitions(isActualSuperAdmin, actorCompanyId);
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

        private List<RoleDefinition> GetAssignableRoleDefinitions(bool isActualSuperAdmin, int? actorCompanyId)
        {
            var query = _uow.Context.RoleDefinitions
                .Include(r => r.Company)
                .Where(r => r.IsActive)
                .AsQueryable();

            if (!isActualSuperAdmin)
            {
                query = query.Where(r => !r.CompanyId.HasValue || r.CompanyId == actorCompanyId);
            }

            return query
                .OrderBy(r => r.CompanyId.HasValue)
                .ThenBy(r => r.Company != null ? r.Company.Name : "Global")
                .ThenBy(r => r.Name)
                .ToList();
        }

        private RoleSelectionResolution ResolveRoleSelection(string selectedRoleKey, bool isActualSuperAdmin, int? actorCompanyId, int? targetCompanyId)
        {
            if (string.IsNullOrWhiteSpace(selectedRoleKey))
            {
                return new RoleSelectionResolution
                {
                    ErrorMessage = "Role selection is required."
                };
            }

            if (selectedRoleKey.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                var builtinRole = selectedRoleKey.Substring("builtin:".Length);
                if (string.Equals(builtinRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && !isActualSuperAdmin)
                {
                    return new RoleSelectionResolution
                    {
                        ErrorMessage = "Only superadmins can assign the SuperAdmin role."
                    };
                }

                return new RoleSelectionResolution
                {
                    IsValid = true,
                    BaseRole = builtinRole,
                    DisplayRoleName = builtinRole
                };
            }

            if (!selectedRoleKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            {
                return new RoleSelectionResolution
                {
                    ErrorMessage = "Unknown role selection."
                };
            }

            if (!int.TryParse(selectedRoleKey.Substring("custom:".Length), out var roleDefinitionId))
            {
                return new RoleSelectionResolution
                {
                    ErrorMessage = "Invalid custom role selection."
                };
            }

            var roleDefinition = _uow.Context.RoleDefinitions
                .Include(r => r.Company)
                .FirstOrDefault(r => r.Id == roleDefinitionId && r.IsActive);

            if (roleDefinition == null)
            {
                return new RoleSelectionResolution
                {
                    ErrorMessage = "The selected custom role no longer exists."
                };
            }

            if (!targetCompanyId.HasValue)
            {
                return new RoleSelectionResolution
                {
                    ErrorMessage = "Custom roles can only be assigned to users that belong to a company."
                };
            }

            if (roleDefinition.CompanyId.HasValue && roleDefinition.CompanyId.Value != targetCompanyId.Value)
            {
                return new RoleSelectionResolution
                {
                    ErrorMessage = string.Format("The custom role '{0}' belongs to a different company scope.", roleDefinition.Name)
                };
            }

            if (!isActualSuperAdmin && roleDefinition.CompanyId.HasValue && roleDefinition.CompanyId.Value != actorCompanyId)
            {
                return new RoleSelectionResolution
                {
                    ErrorMessage = "You cannot assign a custom role from another company."
                };
            }

            return new RoleSelectionResolution
            {
                IsValid = true,
                BaseRole = "Admin",
                RoleDefinitionId = roleDefinition.Id,
                DisplayRoleName = roleDefinition.Name
            };
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
