using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;

namespace HR.Web.Controllers
{
    public class DepartmentsController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly TenantService _tenantService = new TenantService();

        public ActionResult Index()
        {
            var itemsQuery = _uow.Departments.GetAll().AsQueryable();
            itemsQuery = _tenantService.ApplyTenantFilter(itemsQuery);
            var items = itemsQuery.ToList();
            return View(items);
        }

        public ActionResult Details(int id)
        {
            var item = _uow.Departments.Get(id);
            if (item == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && item.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return View(item);
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Create()
        {
            return View(new Department());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Create(Department model)
        {
            // Assign current user's company before validation
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue)
            {
                model.CompanyId = companyId.Value;
                if (ModelState.ContainsKey("CompanyId"))
                {
                    ModelState["CompanyId"].Errors.Clear();
                }
            }
            else
            {
                ModelState.AddModelError("", "No company assigned to your account.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }
            
            _uow.Departments.Add(model);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Edit(int id)
        {
            var item = _uow.Departments.Get(id);
            if (item == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && item.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Edit(Department model)
        {
            // Verify ownership and get existing data
            var existing = _uow.Departments.Get(model.Id);
            if (existing == null) return HttpNotFound();

            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && existing.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }
            
            // Should not change CompanyId on edit
            model.CompanyId = existing.CompanyId;
            
            // Clear validation error for CompanyId as it's not in the form
            if (ModelState.ContainsKey("CompanyId"))
            {
                ModelState["CompanyId"].Errors.Clear();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }
            
            _uow.Departments.Update(model);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult Delete(int id)
        {
            var item = _uow.Departments.Get(id);
            if (item == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && item.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [RoleBasedAuthorization("Admin")]
        public ActionResult DeleteConfirmed(int id)
        {
            var item = _uow.Departments.Get(id);
            if (item == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && item.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            try
            {
                _uow.Departments.Remove(item);
                _uow.Complete();
                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
            {
                // Check if it's a foreign key constraint violation
                if (ex.InnerException != null && ex.InnerException.Message.Contains("REFERENCE constraint"))
                {
                    TempData["Error"] = "Cannot delete this department because it contains positions with existing job applications. Please delete the applications or reassign the positions first.";
                    return RedirectToAction("Delete", new { id = id });
                }
                
                // Handle other database update exceptions
                TempData["Error"] = "An error occurred while deleting the department. Please try again or contact your administrator.";
                return RedirectToAction("Delete", new { id = id });
            }
        }
    }
}









































