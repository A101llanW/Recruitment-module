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
    public class ApplicantsController : Controller
    {
        private readonly UnitOfWork _uow = new UnitOfWork();
        private readonly AuditService _auditService = new AuditService();
        private readonly TenantService _tenantService = new TenantService();

        public ActionResult Index(string sortOrder)
        {
            ViewBag.ProficiencySortParam = string.IsNullOrEmpty(sortOrder) ? "proficiency_desc" : "";
            
            var itemsQuery = _uow.Context.Applicants.Include("Applications").AsQueryable();
            itemsQuery = _tenantService.ApplyTenantFilter(itemsQuery);
            var items = itemsQuery.ToList();
            
            // Sort by proficiency (from latest application's WorkExperienceLevel)
            switch (sortOrder)
            {
                case "proficiency_desc":
                    items = items.OrderByDescending(a => GetProficiencyValue(a)).ToList();
                    break;
                case "proficiency_asc":
                    items = items.OrderBy(a => GetProficiencyValue(a)).ToList();
                    break;
                default:
                    items = items.OrderBy(a => a.FullName).ToList();
                    break;
            }
            
            // Get interviewers for booking (filtered by tenant)
            var interviewersQuery = _uow.Context.Users.Where(u => u.Role == "Admin").AsQueryable();
            interviewersQuery = _tenantService.ApplyTenantFilter(interviewersQuery);
            ViewBag.Interviewers = interviewersQuery.ToList();
            
            // Get existing interview application IDs
            var interviewedAppIds = _uow.Context.Interviews.Select(i => i.ApplicationId).ToList();
            ViewBag.InterviewedAppIds = interviewedAppIds;
            
            return View(items);
        }
        
        private int GetProficiencyValue(Applicant applicant)
        {
            if (applicant.Applications == null || !applicant.Applications.Any())
                return -1;
            var latest = applicant.Applications.OrderByDescending(a => a.AppliedOn).FirstOrDefault();
            if (latest == null || string.IsNullOrEmpty(latest.WorkExperienceLevel))
                return -1;
            int val;
            if (int.TryParse(latest.WorkExperienceLevel, out val))
                return val;
            return -1;
        }

        public ActionResult Details(int id, int? selectedApplicationId = null)
        {
            // Try the original simple approach first
            var applicant = _uow.Applicants.Get(id);
            if (applicant == null)
            {
                return HttpNotFound();
            }

            // Check tenant access
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (companyId.HasValue && applicant.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
            {
                return new HttpStatusCodeResult(403, "Access Denied");
            }
            
            // Get applications for this applicant using a simple direct query
            var applications = _uow.Applications.GetAll()
                .Where(a => a.ApplicantId == id)
                .OrderByDescending(a => a.AppliedOn)
                .ToList();
            
            // Debug: Check what we found
            System.Diagnostics.Debug.WriteLine("Found applicant: " + applicant.FullName + " (ID: " + applicant.Id + ")");
            System.Diagnostics.Debug.WriteLine("Found " + applications.Count + " applications for applicant " + id);
            
            // Default to latest application if none selected
            var selectedApp = selectedApplicationId.HasValue 
                ? applications.FirstOrDefault(a => a.Id == selectedApplicationId.Value)
                : applications.FirstOrDefault();
                
            if (selectedApp != null)
            {
                // Get questionnaire answers for selected application
                var answers = _uow.ApplicationAnswers.GetAll()
                    .Where(aa => aa.ApplicationId == selectedApp.Id)
                    .ToList();
                
                // Load questions explicitly to avoid proxy issues
                var candidateService = new CandidateEvaluationService();
                var answerScores = new Dictionary<int, decimal>();

                foreach (var answer in answers)
                {
                    if (answer.QuestionId > 0)
                    {
                        answer.Question = _uow.Questions.Get(answer.QuestionId);
                    }
                    var positionTitle = selectedApp.Position != null ? selectedApp.Position.Title : "";
                    if (string.IsNullOrEmpty(positionTitle) && selectedApp.PositionId > 0) 
                    {
                         var pos = _uow.Positions.Get(selectedApp.PositionId);
                         positionTitle = pos != null ? pos.Title : "";
                    }
                    answerScores[answer.Id] = candidateService.EvaluateIndividualAnswer(positionTitle, answer.AnswerText);
                }
                
                ViewBag.SelectedApplication = selectedApp;
                ViewBag.QuestionnaireAnswers = answers;
                ViewBag.AnswerScores = answerScores;
                
                System.Diagnostics.Debug.WriteLine("Found " + answers.Count + " answers for application " + selectedApp.Id);
            }
            
            ViewBag.AllApplications = applications;
            ViewBag.SelectedApplicationId = selectedApp != null ? selectedApp.Id : (int?)null;
            
            return View(applicant);
        }

        public ActionResult Create()
        {
            return View(new Applicant());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Applicant model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            
            try
            {
                // Assign company
                var companyId = _tenantService.GetCurrentUserCompanyId();
                if (companyId.HasValue)
                {
                    model.CompanyId = companyId.Value;
                }

                _uow.Applicants.Add(model);
                _uow.Complete();
                
                // Log applicant creation
                _auditService.LogCreate(User.Identity.Name, "Applicants", model.Id.ToString(), new { 
                    FullName = model.FullName, 
                    Email = model.Email, 
                    Phone = model.Phone 
                });
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _auditService.LogAction(User.Identity.Name, "CREATE", "Applicants", "new", 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                ModelState.AddModelError("", "Error creating applicant: " + ex.Message);
                return View(model);
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
            
            try
            {
                // Get old values for audit
                var oldApplicant = _uow.Applicants.Get(model.Id);
                var oldValues = new { 
                    FullName = oldApplicant != null ? oldApplicant.FullName : null, 
                    Email = oldApplicant != null ? oldApplicant.Email : null, 
                    Phone = oldApplicant != null ? oldApplicant.Phone : null 
                };
                
                _uow.Complete();

                // Check tenant access (Double check before update)
                var companyId = _tenantService.GetCurrentUserCompanyId();
                if (companyId.HasValue && model.CompanyId != companyId.Value && !_tenantService.IsSuperAdmin())
                {
                   return new HttpStatusCodeResult(403, "Access Denied");
                }

                // If updating existing, ensure we don't accidentally change CompanyId
                if (oldApplicant != null)
                {
                    model.CompanyId = oldApplicant.CompanyId;
                }
                
                _uow.Applicants.Update(model);
                _uow.Complete();
                
                // Log applicant update
                var newValues = new { 
                    FullName = model.FullName, 
                    Email = model.Email, 
                    Phone = model.Phone 
                };
                
                _auditService.LogUpdate(User.Identity.Name, "Applicants", model.Id.ToString(), oldValues, newValues);
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _auditService.LogAction(User.Identity.Name, "UPDATE", "Applicants", model.Id.ToString(), 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                ModelState.AddModelError("", "Error updating applicant: " + ex.Message);
                return View(model);
            }
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
                    _auditService.LogAction(User.Identity.Name, "DELETE", "Applicants", id.ToString(), 
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
                _auditService.LogDelete(User.Identity.Name, "Applicants", id.ToString(), oldValues);
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _auditService.LogAction(User.Identity.Name, "DELETE", "Applicants", id.ToString(), 
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
                _auditService.LogAction(User.Identity.Name, "DOWNLOAD_CV", "Application", id.ToString(), 
                    new { FileName = fileName, ApplicationId = id });

                return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Octet, fileName);
            }
            catch (Exception ex)
            {
                _auditService.LogAction(User.Identity.Name, "DOWNLOAD_CV_ERROR", "Application", id.ToString(), 
                    wasSuccessful: false, errorMessage: ex.Message);
                
                return new HttpStatusCodeResult(500, "Error downloading file");
            }
        }
    }
}







