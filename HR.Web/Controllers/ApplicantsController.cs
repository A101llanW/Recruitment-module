using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Mvc;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.Filters;
using System.Data.Entity;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "Admin, SuperAdmin")]
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
            LogApplicantDetailsDebug(applicant, applications.Count);

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
            return _uow.Applications.GetAll(a => a.Applicant, a => a.Position)
                .Where(a => a.ApplicantId == applicantId)
                .OrderByDescending(a => a.AppliedOn)
                .ToList();
        }

        private static Application SelectApplication(IEnumerable<Application> applications, int? selectedApplicationId)
        {
            var applicationList = applications ?? Enumerable.Empty<Application>();
            if (selectedApplicationId.HasValue)
            {
                var selectedId = selectedApplicationId.Value;
                return applicationList.FirstOrDefault(a => a != null && a.Id == selectedId);
            }

            return applicationList.FirstOrDefault(a => a != null);
        }

        private static void LogApplicantDetailsDebug(Applicant applicant, int applicationCount)
        {
            if (applicant == null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("Found applicant: " + applicant.FullName + " (ID: " + applicant.Id + ")");
            System.Diagnostics.Debug.WriteLine("Found " + applicationCount + " applications for applicant " + applicant.Id);
        }

        private void PopulateSelectedApplicationViewData(Application selectedApplication)
        {
            if (selectedApplication == null)
            {
                return;
            }

            var answers = GetApplicationAnswers(selectedApplication.Id);
            var answerScores = CalculateAnswerScores(selectedApplication, answers);

            ViewBag.SelectedApplication = selectedApplication;
            ViewBag.QuestionnaireAnswers = answers;
            ViewBag.AnswerScores = answerScores;

            System.Diagnostics.Debug.WriteLine("Found " + answers.Count + " answers for application " + selectedApplication.Id);
        }

        private List<ApplicationAnswer> GetApplicationAnswers(int applicationId)
        {
            return _uow.ApplicationAnswers.GetAll(aa => aa.Question)
                .Where(aa => aa.ApplicationId == applicationId)
                .ToList();
        }

        private Dictionary<int, decimal> CalculateAnswerScores(Application application, IEnumerable<ApplicationAnswer> answers)
        {
            if (application == null || answers == null)
            {
                return new Dictionary<int, decimal>();
            }

            var candidateService = new CandidateEvaluationService();
            var positionTitle = ResolvePositionTitle(application);
            var answerScores = new Dictionary<int, decimal>();

            foreach (var answer in answers)
            {
                if (answer == null)
                {
                    continue;
                }

                LoadAnswerQuestion(answer);
                answerScores[answer.Id] = candidateService.EvaluateIndividualAnswer(positionTitle, answer.AnswerText);
            }

            return answerScores;
        }

        private void LoadAnswerQuestion(ApplicationAnswer answer)
        {
            if (answer == null || answer.QuestionId <= 0 || answer.Question != null)
            {
                return;
            }

            answer.Question = _uow.Questions.Get(answer.QuestionId);
        }

        private string ResolvePositionTitle(Application application)
        {
            if (application == null)
            {
                return string.Empty;
            }

            var scopedApplication = application;
            var positionTitle = scopedApplication.Position != null ? scopedApplication.Position.Title : string.Empty;
            if (!string.IsNullOrEmpty(positionTitle) || scopedApplication.PositionId <= 0)
            {
                return positionTitle;
            }

            var position = _uow.Positions.Get(scopedApplication.PositionId);
            return position != null ? position.Title : string.Empty;
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
                // Do not delete if applicant still has applications (FK constraint)
                var hasApplications = _uow.Applications.GetAll().Any(a => a.ApplicantId == id);
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

        public ActionResult DownloadCV(int id)
        {
            var application = _uow.Applications.Get(id);
            if (application == null || string.IsNullOrEmpty(application.ResumePath))
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && application.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                 return new HttpStatusCodeResult(403, "Access Denied");
            }

            try
            {
                var filePath = Server.MapPath(application.ResumePath);
                if (!System.IO.File.Exists(filePath))
                {
                    return HttpNotFound();
                }

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var fileName = System.IO.Path.GetFileName(filePath);
                
                // Log CV download
                _auditService.LogAction(GetApplicantsActorName(), "DOWNLOAD_CV", "Application", id.ToString(), 
                    new { FileName = fileName, ApplicationId = id });

                return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Octet, fileName);
            }
            catch (Exception ex)
            {
                _auditService.LogAction(GetApplicantsActorName(), "DOWNLOAD_CV_ERROR", "Application", id.ToString(), 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                return new HttpStatusCodeResult(500, "Error downloading file");
            }
        }
    }
}




