using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Models;
using Newtonsoft.Json;

namespace HR.Web.Controllers
{
    public partial class CompaniesController
    {
        private sealed class CompanyAdminCredentialBundle
        {
            public User AdminUser { get; set; }
            public string TempPassword { get; set; }
            public string DownloadToken { get; set; }
        }

        private ActionResult HandleCreateCompany(CreateCompanyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var formKey = BuildCreateCompanyFormKey(model);
            if (IsCreateCompanySubmissionLocked(formKey))
            {
                TempData["ErrorMessage"] = "Duplicate submission detected. Company creation is already in progress.";
                return RedirectToAction("Index");
            }

            LockCreateCompanySubmission(formKey);

            try
            {
                var validationResult = ValidateCreateCompanyInput(model, formKey);
                if (validationResult != null)
                {
                    return validationResult;
                }

                return CompleteCompanyCreation(model, formKey);
            }
            catch (DbEntityValidationException dbEx)
            {
                ReleaseCreateCompanySubmission(formKey);
                ModelState.AddModelError("", BuildEntityValidationErrorMessage(dbEx));
                return View(model);
            }
            catch (Exception ex)
            {
                ReleaseCreateCompanySubmission(formKey);
                ModelState.AddModelError("", "Error creating company: " + FlattenExceptionMessages(ex));
                return View(model);
            }
        }

        private static string BuildCreateCompanyFormKey(CreateCompanyViewModel model)
        {
            return string.Format(
                "CreateCompany_{0}_{1}",
                System.Web.HttpContext.Current.User.Identity.Name,
                model.Name.ToLower().Trim());
        }

        private static bool IsCreateCompanySubmissionLocked(string formKey)
        {
            return HttpRuntime.Cache[formKey] != null;
        }

        private static void LockCreateCompanySubmission(string formKey)
        {
            HttpRuntime.Cache.Insert(
                formKey,
                true,
                null,
                DateTime.Now.AddSeconds(10),
                System.Web.Caching.Cache.NoSlidingExpiration);
        }

        private static void ReleaseCreateCompanySubmission(string formKey)
        {
            HttpRuntime.Cache.Remove(formKey);
        }

        private ActionResult ValidateCreateCompanyInput(CreateCompanyViewModel model, string formKey)
        {
            var normalizedName = model.Name.Trim().ToLower();
            var existingExact = FindCompanyByExactName(normalizedName);
            if (existingExact != null)
            {
                ModelState.AddModelError("Name", "A company with this exact name already exists.");
                ReleaseCreateCompanySubmission(formKey);
                return View(model);
            }

            if (model.IgnoreSimilarNameWarning)
            {
                return null;
            }

            var similarCompany = FindSimilarCompanyByName(normalizedName);
            if (similarCompany == null)
            {
                return null;
            }

            ModelState.AddModelError(
                "Name",
                string.Format(
                    "Warning: A company with a similar name ('{0}') already exists. If you are sure you want to create this company, check the confirmation box below and submit again.",
                    similarCompany.Name));
            ReleaseCreateCompanySubmission(formKey);
            return View(model);
        }

        private Company FindCompanyByExactName(string normalizedName)
        {
            return _uow.Companies.GetAll().FirstOrDefault(c => c.Name.ToLower() == normalizedName);
        }

        private Company FindSimilarCompanyByName(string normalizedName)
        {
            var allCompanies = _uow.Companies.GetAll().ToList();
            return allCompanies.FirstOrDefault(c =>
                c.Name.ToLower() != normalizedName &&
                c.Name.Length > 2 &&
                normalizedName.Length > 2 &&
                (c.Name.ToLower().Contains(normalizedName) || normalizedName.Contains(c.Name.ToLower())));
        }

        private ActionResult CompleteCompanyCreation(CreateCompanyViewModel model, string formKey)
        {
            var company = _tenantService.CreateCompany(model.Name, model.LicenseExpiryDate ?? DateTime.Now.AddYears(1));
            LogCompanyCreation(company);

            var bundle = PrepareCompanyAdminCredentials(company);
            if (bundle != null)
            {
                SaveTemporaryCredential(bundle, company);
                TempData["CredentialDownloadToken"] = bundle.DownloadToken;
                TempData["NewCompanyName"] = company.Name;
            }

            ReleaseCreateCompanySubmission(formKey);
            SetCompanyCreateSuccessMessage(company, bundle);
            return RedirectToAction("Index");
        }

        private void LogCompanyCreation(Company company)
        {
            _auditService.LogAction(
                User.Identity.Name,
                "COMPANY_CREATED",
                "Company",
                company.Id.ToString(),
                null,
                new { CompanyName = company.Name, Slug = company.Slug });
        }

        private CompanyAdminCredentialBundle PrepareCompanyAdminCredentials(Company company)
        {
            var adminUser = _uow.Users.GetAll()
                .Where(u => u.CompanyId == company.Id)
                .OrderByDescending(u => u.Id)
                .FirstOrDefault();
            if (adminUser == null)
            {
                return null;
            }

            var tempPassword = _tenantService.GenerateDefaultPassword();
            adminUser.PasswordHash = PasswordHelper.HashPassword(tempPassword);
            _uow.Users.Update(adminUser);
            _uow.Complete();

            return new CompanyAdminCredentialBundle
            {
                AdminUser = adminUser,
                TempPassword = tempPassword,
                DownloadToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")
            };
        }

        private void SaveTemporaryCredential(CompanyAdminCredentialBundle bundle, Company company)
        {
            var credentials = new AdminCredentialsViewModel
            {
                CompanyName = company.Name,
                CompanyUrl = string.Format("{0}/{1}", ExternalUrlHelper.GetBaseUrl(Request), company.Slug),
                AdminUsername = bundle.AdminUser.UserName,
                AdminPassword = bundle.TempPassword
            };

            var jsonData = JsonConvert.SerializeObject(credentials);
            var encryptedData = EncryptionHelper.Encrypt(jsonData);

            _uow.TemporaryCredentials.Add(
                new TemporaryCredential
                {
                    Token = bundle.DownloadToken,
                    EncryptedData = encryptedData,
                    ExpiryDate = DateTime.Now.AddHours(1),
                    IsUsed = false,
                    CreatedDate = DateTime.Now,
                    CredentialType = "CompanyAdmin"
                });
            _uow.Complete();
        }

        private void SetCompanyCreateSuccessMessage(Company company, CompanyAdminCredentialBundle bundle)
        {
            var downloadStatus = bundle != null
                ? "CREDENTIALS_READY"
                : "CREDENTIALS_MISSING (User:False, Pwd:False)";

            TempData["SuccessMessage"] = string.Format(
                "Company '{0}' created successfully! URL: <strong>{1}/{2}</strong><br/><small class='text-muted'>Status: {3}</small>",
                company.Name,
                ExternalUrlHelper.GetBaseUrl(Request),
                company.Slug,
                downloadStatus);
        }

        private static string BuildEntityValidationErrorMessage(DbEntityValidationException dbEx)
        {
            var messages = dbEx.EntityValidationErrors
                .SelectMany(x => x.ValidationErrors)
                .Select(x => x.PropertyName + ": " + x.ErrorMessage);
            return "Validation failed: " + string.Join("; ", messages);
        }

        private static string FlattenExceptionMessages(Exception ex)
        {
            var errorBuilder = new StringBuilder(ex.Message);
            var inner = ex.InnerException;
            while (inner != null)
            {
                errorBuilder.Append(" | Inner: ");
                errorBuilder.Append(inner.Message);
                inner = inner.InnerException;
            }

            return errorBuilder.ToString();
        }

        private ActionResult HandleDownloadCredentials(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return HttpNotFound();
            }

            var credential = GetValidCredential(token);
            if (credential == null)
            {
                return Content("This download link is invalid, has expired, or has already been used. For security, admin credentials can only be downloaded once.");
            }

            MarkCredentialAsUsed(credential);
            LogCredentialDownload(token, credential);

            var data = DecryptCredentialData(credential.EncryptedData);
            var fileBytes = BuildCredentialFileContent(data, credential);
            var fileName = string.Format("Credentials_{0}_{1}.txt", data.CompanyName.Replace(" ", "_"), DateTime.Now.ToString("yyyyMMdd"));
            return File(fileBytes, "text/plain", fileName);
        }

        private TemporaryCredential GetValidCredential(string token)
        {
            return _uow.TemporaryCredentials.GetAll()
                .FirstOrDefault(tc => tc.Token == token && !tc.IsUsed && tc.ExpiryDate > DateTime.Now);
        }

        private void MarkCredentialAsUsed(TemporaryCredential credential)
        {
            credential.IsUsed = true;
            _uow.TemporaryCredentials.Update(credential);
            _uow.Complete();
        }

        private void LogCredentialDownload(string token, TemporaryCredential credential)
        {
            _auditService.LogAction(
                GetCredentialDownloadActor(),
                "CREDENTIALS_DOWNLOADED",
                "TemporaryCredential",
                credential.Id.ToString(),
                null,
                new { TokenUsed = token.Substring(0, 8) + "..." });
        }

        private string GetCredentialDownloadActor()
        {
            return User.Identity != null && User.Identity.IsAuthenticated
                ? User.Identity.Name
                : "Anonymous";
        }

        private static AdminCredentialsViewModel DecryptCredentialData(string encryptedData)
        {
            var decryptedJson = EncryptionHelper.Decrypt(encryptedData);
            return JsonConvert.DeserializeObject<AdminCredentialsViewModel>(decryptedJson);
        }

        private ActionResult HandleCompanyDetails(int id)
        {
            var company = _uow.Companies.Get(id);
            if (company == null)
            {
                return HttpNotFound();
            }

            SetRejectionNotificationIfPresent(id);
            var viewModel = BuildCompanyDetailsViewModel(company, id);
            return View(viewModel);
        }

        private void SetRejectionNotificationIfPresent(int companyId)
        {
            var rejectionNotification = Session["RejectionNotification"] as dynamic;
            if (rejectionNotification == null || rejectionNotification.CompanyId != companyId)
            {
                return;
            }

            ViewBag.RejectionNotification = rejectionNotification;
            Session["RejectionNotification"] = null;
        }

        private CompanyDetailsViewModel BuildCompanyDetailsViewModel(Company company, int companyId)
        {
            return new CompanyDetailsViewModel
            {
                Company = company,
                Users = _uow.Users.GetAll().Where(u => u.CompanyId == companyId).ToList(),
                Positions = _uow.Positions.GetAll().Where(p => p.CompanyId == companyId).ToList(),
                Applications = _uow.Applications.GetAll().Where(a => a.CompanyId == companyId).ToList(),
                Departments = _uow.Departments.GetAll().Where(d => d.CompanyId == companyId).ToList(),
                LicenseTransactions = _uow.LicenseTransactions.GetAll()
                    .Where(lt => lt.CompanyId == companyId)
                    .OrderByDescending(lt => lt.TransactionDate)
                    .ToList(),
                RecentAuditLogs = _uow.AuditLogs.GetAll()
                    .Where(a => a.CompanyId == companyId)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(20)
                    .ToList(),
                PendingImpersonationRequests = _uow.ImpersonationRequests.GetAll()
                    .Where(r => r.CompanyId == companyId && r.Status == ImpersonationRequestStatus.Pending && r.RequestedBy == User.Identity.Name)
                    .OrderByDescending(r => r.RequestDate)
                    .ToList(),
                ActiveApprovedRequest = _uow.ImpersonationRequests.GetAll()
                    .FirstOrDefault(r =>
                        r.CompanyId == companyId &&
                        r.RequestedBy == User.Identity.Name &&
                        r.Status == ImpersonationRequestStatus.Approved &&
                        (!r.ExpiryDate.HasValue || r.ExpiryDate > DateTime.Now)),
                ActiveRejectedRequest = null,
                CompanyAdmins = _uow.Users.GetAll()
                    .Where(u => u.CompanyId == companyId && u.Role == "Admin")
                    .ToList()
            };
        }

        private ActionResult HandleElevate(int requestId)
        {
            var request = _uow.ImpersonationRequests.Get(requestId);
            var requestValidationResult = ValidateElevationRequest(request);
            if (requestValidationResult != null)
            {
                return requestValidationResult;
            }

            var company = _uow.Companies.Get(request.CompanyId.Value);
            if (company == null)
            {
                return HttpNotFound();
            }

            var expiryResult = ValidateElevationExpiry(request);
            if (expiryResult != null)
            {
                return expiryResult;
            }

            ActivateImpersonation(request, company);
            return RedirectToAction("Index", "Dashboard");
        }

        private ActionResult ValidateElevationRequest(ImpersonationRequest request)
        {
            if (request == null || request.RequestedBy != User.Identity.Name)
            {
                return HttpNotFound();
            }

            if (request.Status == ImpersonationRequestStatus.Approved)
            {
                return null;
            }

            TempData["ErrorMessage"] = "This request has not been approved or has expired.";
            return RedirectToAction("CompanyDetails", new { id = request.CompanyId });
        }

        private ActionResult ValidateElevationExpiry(ImpersonationRequest request)
        {
            if (!request.ExpiryDate.HasValue || request.ExpiryDate.Value >= DateTime.Now)
            {
                return null;
            }

            request.Status = ImpersonationRequestStatus.Expired;
            _uow.ImpersonationRequests.Update(request);
            _uow.Complete();
            TempData["ErrorMessage"] = "This authorization has expired.";
            return RedirectToAction("CompanyDetails", new { id = request.CompanyId });
        }

        private void ActivateImpersonation(ImpersonationRequest request, Company company)
        {
            _auditService.LogAction(
                User.Identity.Name,
                "IMPERSONATION_START",
                "Companies",
                request.CompanyId.ToString(),
                null,
                new { Reason = request.Reason, CompanyName = company.Name, ApprovedBy = request.RequestedFrom });

            Session["ImpersonatedRequestId"] = request.Id;
            Session["ImpersonatedCompanyId"] = request.CompanyId;
            Session["ImpersonationReason"] = request.Reason ?? "Not specified";
            Session["ImpersonatedCompanyName"] = company.Name;
            Session["ImpersonationExpiry"] = request.ExpiryDate;

            request.Status = ImpersonationRequestStatus.Active;
            _uow.ImpersonationRequests.Update(request);
            _uow.Complete();

            TempData["SuccessMessage"] = string.Format(
                "Now impersonating {0}. Session authorized by {1}. Expiry: {2}",
                company.Name,
                request.RequestedFrom,
                request.ExpiryDate.HasValue ? request.ExpiryDate.Value.ToString("HH:mm") : "N/A");
        }

        private ActionResult HandleDeleteCompany(int id)
        {
            try
            {
                var company = _uow.Companies.Get(id);
                if (company == null)
                {
                    return HttpNotFound();
                }

                var dependencyResult = ValidateCompanyDeletionDependencies(id);
                if (dependencyResult != null)
                {
                    return dependencyResult;
                }

                DeleteCompanyUsers(id);
                DeleteCompanyLinkedRecords(id);

                var companyName = company.Name;
                _uow.Companies.Remove(company);
                _uow.Complete();

                _auditService.LogAction(
                    User.Identity.Name,
                    "COMPANY_DELETED",
                    "Company",
                    id.ToString(),
                    new { Name = companyName },
                    null);

                TempData["SuccessMessage"] = string.Format("Company '{0}' has been permanently deleted.", companyName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting company: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        private ActionResult ValidateCompanyDeletionDependencies(int companyId)
        {
            var positionCount = _uow.Positions.GetAll().Count(p => p.CompanyId == companyId);
            var appCount = _uow.Applications.GetAll().Count(a => a.CompanyId == companyId);
            if (positionCount <= 0 && appCount <= 0)
            {
                return null;
            }

            TempData["ErrorMessage"] = "Cannot delete company because it has existing positions or applications.";
            return RedirectToAction("Index");
        }

        private void DeleteCompanyUsers(int companyId)
        {
            var users = _uow.Users.GetAll().Where(u => u.CompanyId == companyId).ToList();
            foreach (var user in users)
            {
                DeleteUserDependencies(user);
                _uow.Users.Remove(user);
            }
        }

        private void DeleteUserDependencies(User user)
        {
            var impersonations = _uow.Context.ImpersonationRequests.Where(r => r.RequestedFrom == user.UserName || r.RequestedBy == user.UserName);
            _uow.Context.ImpersonationRequests.RemoveRange(impersonations);

            var resets = _uow.Context.PasswordResets.Where(p => p.UserId == user.Id);
            _uow.Context.PasswordResets.RemoveRange(resets);

            var loginAttempts = _uow.Context.LoginAttempts.Where(l => l.Username == user.UserName);
            _uow.Context.LoginAttempts.RemoveRange(loginAttempts);

            var userAuditLogs = _uow.Context.AuditLogs.Where(a => a.Username == user.UserName);
            _uow.Context.AuditLogs.RemoveRange(userAuditLogs);
        }

        private void DeleteCompanyLinkedRecords(int companyId)
        {
            _uow.Context.Applicants.RemoveRange(_uow.Context.Applicants.Where(a => a.CompanyId == companyId));
            _uow.Context.Departments.RemoveRange(_uow.Context.Departments.Where(d => d.CompanyId == companyId));
            DeleteCompanyQuestions(companyId);
            _uow.Context.AuditLogs.RemoveRange(_uow.Context.AuditLogs.Where(a => a.CompanyId == companyId));
            _uow.Context.LicenseTransactions.RemoveRange(_uow.Context.LicenseTransactions.Where(t => t.CompanyId == companyId));
        }

        private void DeleteCompanyQuestions(int companyId)
        {
            var questions = _uow.Context.Questions.Where(q => q.CompanyId == companyId).ToList();
            foreach (var question in questions)
            {
                var options = _uow.Context.QuestionOptions.Where(qo => qo.QuestionId == question.Id);
                _uow.Context.QuestionOptions.RemoveRange(options);
                _uow.Context.Questions.Remove(question);
            }
        }
    }
}
