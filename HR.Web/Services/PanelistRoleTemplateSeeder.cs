using System;
using System.Linq;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Services
{
    /// <summary>
    /// Ensures each company has a default Panelist role template (read-only interviews and applications)
    /// for assigning <see cref="User.Role"/> = Panelist with module-scoped access.
    /// </summary>
    public static class PanelistRoleTemplateSeeder
    {
        public const string DefaultTemplateName = "Panelist";

        public static void EnsureForCompany(UnitOfWork uow, int companyId, string createdByUserName)
        {
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }

            var ctx = uow.Context;
            var exists = ctx.RoleDefinitions.Any(r =>
                r.IsActive &&
                r.CompanyId == companyId &&
                r.Name == DefaultTemplateName);

            if (exists)
            {
                return;
            }

            var role = new RoleDefinition
            {
                CompanyId = companyId,
                Name = DefaultTemplateName,
                Description = "Starter panelist permissions (adjust in Role Management). Default is read-only Interviews and Applications.",
                CreatedByUserName = string.IsNullOrWhiteSpace(createdByUserName) ? "system" : createdByUserName.Trim(),
                CreatedDate = DateTime.Now,
                IsActive = true
            };

            ctx.RoleDefinitions.Add(role);
            uow.Complete();

            ctx.RolePermissions.Add(new RolePermission
            {
                RoleDefinitionId = role.Id,
                ModuleKey = RoleModuleCatalog.Interviews,
                AccessLevel = RoleAccessLevels.View
            });
            ctx.RolePermissions.Add(new RolePermission
            {
                RoleDefinitionId = role.Id,
                ModuleKey = RoleModuleCatalog.Applications,
                AccessLevel = RoleAccessLevels.View
            });
            uow.Complete();
        }
    }
}
