using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;
using System.Data.Entity;

namespace HR.Web.Controllers
{
    [TenantAuthorize(Roles = "Admin, SuperAdmin")]
    [RoleBasedAuthorization("Admin")]
    [ModuleAccess(RoleModuleCatalog.Applicants)]
    public class ApplicantsController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly AuditService _auditService = new AuditService();
        private readonly TenantService _tenantService = new TenantService();

        private string GetApplicantsActorName()
        {
            return User?.Identity?.Name ?? "System";
        }

        public ActionResult Index()
        {
            var itemsQuery = _uow.Context.Applicants.AsQueryable();
            itemsQuery = _tenantService.ApplyTenantFilter(itemsQuery);
            var items = itemsQuery
                .OrderBy(a => a.FullName)
                .ToList();

            return View(items);
        }

        public ActionResult Details(int id, int? selectedApplicationId = null)
        {
            var applicant = _uow.Applicants.Get(id);
            if (applicant == null)
            {
                return HttpNotFound();
            }

            var tenantAccessResult = EnsureApplicantTenantAccess(applicant);
            if (tenantAccessResult != null)
            {
                return tenantAccessResult;
            }

            var applications = GetApplicantApplications(id) ?? new List<Application>();

            var selectedApp = SelectApplication(applications, selectedApplicationId);
            PopulateSelectedApplicationViewData(selectedApp);
            ViewBag.AllApplications = applications;
            ViewBag.SelectedApplicationId = selectedApp != null ? selectedApp.Id : (int?)null;

            return View(applicant);
        }

        private ActionResult EnsureApplicantTenantAccess(Applicant applicant)
        {
            if (applicant == null)
            {
                return HttpNotFound();
            }

            var scopedApplicant = applicant;
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && scopedApplicant.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return null;
        }

        private List<Application> GetApplicantApplications(int applicantId)
        {
            var appsQuery = _uow.Applications.GetAll(a => a.Applicant, a => a.Position)
                .Where(a => a.ApplicantId == applicantId);
            appsQuery = _tenantService.ApplyTenantFilter(appsQuery);
            return appsQuery
                .OrderByDescending(a => a.AppliedOn)
                .ToList();
        }

        private static Application SelectApplication(IEnumerable<Application> applications, int? selectedApplicationId)
        {
            var applicationList = (applications ?? Enumerable.Empty<Application>())
                .Where(a => a != null)
                .ToList();

            if (!applicationList.Any())
            {
                return null;
            }

            if (selectedApplicationId.HasValue)
            {
                var selected = applicationList.FirstOrDefault(a => a.Id == selectedApplicationId.Value);
                if (selected != null)
                {
                    return selected;
                }
            }

            return applicationList.FirstOrDefault();
        }

        private void PopulateSelectedApplicationViewData(Application selectedApplication)
        {
            if (selectedApplication == null)
            {
                return;
            }

            if (!IsApplicationTenantAccessible(selectedApplication))
            {
                ViewBag.SelectedApplication = null;
                ViewBag.QuestionnaireAnswers = new List<ApplicationAnswer>();
                return;
            }

            var answers = GetApplicationAnswers(selectedApplication);

            ViewBag.SelectedApplication = selectedApplication;
            ViewBag.QuestionnaireAnswers = answers;
        }

        private bool IsApplicationTenantAccessible(Application application)
        {
            if (application == null)
            {
                return false;
            }

            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (!companyId.HasValue || _tenantService.IsSuperAdmin())
            {
                return true;
            }

            return application.CompanyId == companyId.Value;
        }

        private List<ApplicationAnswer> GetApplicationAnswers(Application application)
        {
            if (application == null || !IsApplicationTenantAccessible(application))
            {
                return new List<ApplicationAnswer>();
            }

            return _uow.ApplicationAnswers.GetAll(aa => aa.Question)
                .Where(aa => aa.ApplicationId == application.Id)
                .ToList();
        }

        public ActionResult Create()
        {
            return View(new Applicant());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Applicant model)
        {
            if (model == null)
            {
                return View(new Applicant());
            }

            var applicantModel = model;
            if (!ModelState.IsValid)
            {
                return View(applicantModel);
            }
            
            try
            {
                var companyId = _tenantService.GetCurrentUserCompanyId();
                if (companyId.HasValue)
                {
                    applicantModel.CompanyId = companyId.Value;
                }

                _uow.Applicants.Add(applicantModel);
                _uow.Complete();
                
                _auditService.LogCreate(GetApplicantsActorName(), "Applicants", applicantModel.Id.ToString(), new { 
                    FullName = applicantModel.FullName, 
                    Email = applicantModel.Email, 
                    Phone = applicantModel.Phone 
                });
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _auditService.LogAction(GetApplicantsActorName(), "CREATE", "Applicants", "new", 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                ModelState.AddModelError("", "Error creating applicant: " + ex.Message);
                return View(applicantModel);
            }
        }

        public ActionResult Edit(int id)
        {
            var item = _uow.Applicants.Get(id);
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
        public ActionResult Edit(Applicant model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            return HandleApplicantEdit(model);
        }

        private ActionResult HandleApplicantEdit(Applicant model)
        {
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            var applicantModel = model;
            try
            {
                var oldApplicant = _uow.Applicants.Get(applicantModel.Id);
                var accessResult = EnsureApplicantModelTenantAccess(applicantModel);
                if (accessResult != null)
                {
                    return accessResult;
                }

                PreserveApplicantCompany(oldApplicant, applicantModel);
                _uow.Applicants.Update(applicantModel);
                _uow.Complete();

                var oldValues = BuildApplicantAuditValues(oldApplicant);
                var newValues = BuildApplicantAuditValues(applicantModel);
                _auditService.LogUpdate(GetApplicantsActorName(), "Applicants", applicantModel.Id.ToString(), oldValues, newValues);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _auditService.LogAction(GetApplicantsActorName(), "UPDATE", "Applicants", applicantModel.Id.ToString(), 
                    wasSuccessful: false, errorMessage: ex.Message);

                ModelState.AddModelError("", "Error updating applicant: " + ex.Message);
                return View(applicantModel);
            }
        }

        private ActionResult EnsureApplicantModelTenantAccess(Applicant model)
        {
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            var applicantModel = model;
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && applicantModel.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            return null;
        }

        private static void PreserveApplicantCompany(Applicant oldApplicant, Applicant model)
        {
            if (oldApplicant != null)
            {
                model.CompanyId = oldApplicant.CompanyId;
            }
        }

        private static object BuildApplicantAuditValues(Applicant applicant)
        {
            return new
            {
                FullName = applicant != null ? applicant.FullName : null,
                Email = applicant != null ? applicant.Email : null,
                Phone = applicant != null ? applicant.Phone : null
            };
        }

        public ActionResult Delete(int id)
        {
            var item = _uow.Applicants.Get(id);
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
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                // Do not delete if applicant still has applications in this tenant (FK constraint)
                var appsQuery = _uow.Applications.GetAll().Where(a => a.ApplicantId == id);
                appsQuery = _tenantService.ApplyTenantFilter(appsQuery);
                var hasApplications = appsQuery.Any();
                if (hasApplications)
                {
                    TempData["DeleteError"] = "Cannot delete applicant because applications still exist. Delete or reassign those applications first.";
                    
                    // Log failed deletion attempt
                    _auditService.LogAction(GetApplicantsActorName(), "DELETE", "Applicants", id.ToString(), 
                        wasSuccessful: false, errorMessage: "Applicant has existing applications");
                    
                    return RedirectToAction("Details", new { id });
                }

                var item = _uow.Applicants.Get(id);
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
                
                // Store old values for audit
                var oldValues = new { 
                    FullName = item.FullName, 
                    Email = item.Email, 
                    Phone = item.Phone 
                };
                
                _uow.Applicants.Remove(item);
                _uow.Complete();
                
                // Log successful deletion
                _auditService.LogDelete(GetApplicantsActorName(), "Applicants", id.ToString(), oldValues);
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _auditService.LogAction(GetApplicantsActorName(), "DELETE", "Applicants", id.ToString(), 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                TempData["DeleteError"] = "Error deleting applicant: " + ex.Message;
                return RedirectToAction("Details", new { id });
            }
        }

        public ActionResult DownloadCoverLetter(int id)
        {
            var application = _uow.Applications.GetAll(a => a.Applicant, a => a.Position)
                .FirstOrDefault(a => a.Id == id);
            if (application == null || string.IsNullOrWhiteSpace(application.CoverLetter))
            {
                return HttpNotFound();
            }

            if (!IsApplicationTenantAccessible(application))
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }

            try
            {
                var applicantName = application.Applicant != null
                    ? application.Applicant.FullName
                    : "Applicant";
                var positionTitle = application.Position != null
                    ? application.Position.Title
                    : "Position";
                var fileName = BuildCoverLetterDownloadFileName(applicantName, positionTitle, application.Id);
                var fileBytes = Encoding.UTF8.GetBytes(application.CoverLetter);

                _auditService.LogAction(GetApplicantsActorName(), "DOWNLOAD_COVER_LETTER", "Application", id.ToString(),
                    new { FileName = fileName, ApplicationId = id });

                return File(fileBytes, "text/plain", fileName);
            }
            catch (Exception ex)
            {
                _auditService.LogAction(GetApplicantsActorName(), "DOWNLOAD_COVER_LETTER_ERROR", "Application", id.ToString(),
                    wasSuccessful: false, errorMessage: ex.Message);

                return new HttpStatusCodeResult(500, "Error downloading file");
            }
        }

        private static string BuildCoverLetterDownloadFileName(string applicantName, string positionTitle, int applicationId)
        {
            var safeApplicant = SanitizeDownloadFileSegment(applicantName, "Applicant");
            var safePosition = SanitizeDownloadFileSegment(positionTitle, "Position");
            return string.Format("CoverLetter_{0}_{1}_{2}.txt", safeApplicant, safePosition, applicationId);
        }

        private static string SanitizeDownloadFileSegment(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var chars = value.Trim()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray();
            var sanitized = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
        }
    }
}




