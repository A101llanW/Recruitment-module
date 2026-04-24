using System;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "Admin, SuperAdmin")]
    [RoleBasedAuthorization("Admin")]
    [ModuleAccess(RoleModuleCatalog.Onboardings)]
    public class OnboardingsController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly Services.TenantService _tenantService = new Services.TenantService();

        public ActionResult Index()
        {
            var query = _uow.Onboardings.GetAll(o => o.Application.Applicant, o => o.Application.Position).AsQueryable();
            
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && !_tenantService.IsSuperAdmin())
            {
                query = query.Where(o => o.Application.CompanyId == companyId.Value);
            }
            
            return View(query.ToList());
        }

        public ActionResult Details(int id)
        {
            var item = _uow.Onboardings.GetAll(o => o.Application.Applicant, o => o.Application.Position)
                .FirstOrDefault(o => o.Id == id);
            
            if (item == null)
            {
                return HttpNotFound();
            }

            // Tenant check
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && item.Application.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return View(item);
        }

        public ActionResult Create()
        {
            var applicationsQuery = _uow.Applications.GetAll(a => a.Applicant, a => a.Position).AsQueryable();
            applicationsQuery = _tenantService.ApplyTenantFilter(applicationsQuery);

            ViewBag.ApplicationId = new SelectList(applicationsQuery.ToList(), "Id", "Id");
            return View(new Onboarding { Status = "Pending", StartDate = DateTime.UtcNow.AddDays(7) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Onboarding model)
        {
            // Verify application belongs to tenant
            var application = _uow.Applications.Get(model.ApplicationId);
            if (application != null)
            {
                var companyId = _tenantService.GetCurrentUserCompanyId();
                if (companyId.HasValue && application.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
                {
                     ModelState.AddModelError("", "Invalid application selected.");
                }
            }

            if (!ModelState.IsValid)
            {
                var applicationsQuery = _uow.Applications.GetAll(a => a.Applicant, a => a.Position).AsQueryable();
                applicationsQuery = _tenantService.ApplyTenantFilter(applicationsQuery);
                ViewBag.ApplicationId = new SelectList(applicationsQuery.ToList(), "Id", "Id", model.ApplicationId);
                return View(model);
            }
            _uow.Onboardings.Add(model);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        public ActionResult Edit(int id)
        {
            var item = _uow.Onboardings.GetAll(o => o.Application).FirstOrDefault(o => o.Id == id);
            if (item == null)
            {
                return HttpNotFound();
            }
            
            // Tenant check
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && item.Application.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }
            
            var applicationsQuery = _uow.Applications.GetAll(a => a.Applicant, a => a.Position).AsQueryable();
            applicationsQuery = _tenantService.ApplyTenantFilter(applicationsQuery);
            ViewBag.ApplicationId = new SelectList(applicationsQuery.ToList(), "Id", "Id", item.ApplicationId);
            
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Onboarding model)
        {
            // Verify ownership
            var existing = _uow.Onboardings.GetAll(o => o.Application).FirstOrDefault(o => o.Id == model.Id);
            if (existing == null) return HttpNotFound();
            
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && existing.Application.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            if (!ModelState.IsValid)
            {
                var applicationsQuery = _uow.Applications.GetAll(a => a.Applicant, a => a.Position).AsQueryable();
                applicationsQuery = _tenantService.ApplyTenantFilter(applicationsQuery);
                ViewBag.ApplicationId = new SelectList(applicationsQuery.ToList(), "Id", "Id", model.ApplicationId);
                return View(model);
            }

            // Update allowed fields
            existing.StartDate = model.StartDate;
            existing.Status = model.Status;
            existing.Tasks = model.Tasks;
            // distinct from model.ApplicationId which shouldn't change easily or needs verification if it does
            
            _uow.Onboardings.Update(existing);
            _uow.Complete();
            return RedirectToAction("Index");
        }

        public ActionResult Delete(int id)
        {
            var item = _uow.Onboardings.GetAll(o => o.Application).FirstOrDefault(o => o.Id == id);
            if (item == null)
            {
                return HttpNotFound();
            }

            // Tenant check
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && item.Application.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }
            
            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var item = _uow.Onboardings.GetAll(o => o.Application).FirstOrDefault(o => o.Id == id);
            if (item == null)
            {
                return HttpNotFound();
            }

            // Tenant check
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && item.Application.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            _uow.Onboardings.Remove(item);
            _uow.Complete();
            return RedirectToAction("Index");
        }
    }
}










































