using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HR.Web.Filters;
using HR.Web.Models;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "Admin, SuperAdmin")]
    [RoleBasedAuthorization("Admin")]
    [ModuleAccess(RoleModuleCatalog.Reports)]
    public class ReportGeneratorController : Controller
    {
        private readonly ReportService _reportService = new ReportService();

        public ActionResult Index()
        {
            using (var service = new CustomReportService())
            {
                var model = new CustomReportPageViewModel
                {
                    Saved = service.List(1),
                    CanManage = new RolePermissionService().CanCurrentUserAccessModule(
                        RoleModuleCatalog.Reports, RoleAccessLevels.Manage)
                };
                return View("~/Views/Reports/Index.cshtml", model);
            }
        }

        public ActionResult SavedPage(int page)
        {
            using (var service = new CustomReportService())
            {
                ViewData["CanManage"] = new RolePermissionService().CanCurrentUserAccessModule(
                    RoleModuleCatalog.Reports, RoleAccessLevels.Manage);
                return PartialView("~/Views/Reports/_SavedCustomReports.cshtml", service.List(page));
            }
        }

        public ActionResult Builder()
        {
            using (var service = new CustomReportService())
            {
                return View("~/Views/Reports/Builder.cshtml", BuildEmptyBuilderViewModel(service));
            }
        }

        public ActionResult Edit(int id, int? saved)
        {
            using (var service = new CustomReportService())
            {
                var definition = service.GetForCurrentCompany(id);
                if (definition == null)
                {
                    return HttpNotFound();
                }

                ValidatedCustomReport validated;
                string error;
                if (!CustomReportConfigValidator.TryValidate(
                    definition.Name,
                    definition.Description,
                    definition.ConfigJson,
                    service.PositionIdsBelongToCompany,
                    service.DepartmentIdsBelongToCompany,
                    out validated,
                    out error))
                {
                    var invalidModel = BuildEmptyBuilderViewModel(service);
                    invalidModel.Id = id;
                    invalidModel.Name = definition.Name;
                    invalidModel.Description = definition.Description;
                    invalidModel.Error = error;
                    invalidModel.SavedFlash = saved.HasValue && saved.Value == 1;
                    return View("~/Views/Reports/Builder.cshtml", invalidModel);
                }

                var model = BuildBuilderViewModelFromValidated(service, validated, id);
                model.SavedFlash = saved.HasValue && saved.Value == 1;
                return View("~/Views/Reports/Builder.cshtml", model);
            }
        }

        public ActionResult FilterOptions(string kind, int page)
        {
            using (var service = new CustomReportService())
            {
                var result = service.GetFilterOptions(kind, page);
                return Json(new
                {
                    items = result.Items.Select(i => new { id = i.Id, label = i.Label }),
                    hasMore = result.HasMore
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PreviewCustom(string name, string description, string configJson, int page)
        {
            using (var service = new CustomReportService())
            {
                ValidatedCustomReport validated;
                string error;
                if (!CustomReportConfigValidator.TryValidate(
                    name,
                    description,
                    configJson,
                    service.PositionIdsBelongToCompany,
                    service.DepartmentIdsBelongToCompany,
                    out validated,
                    out error))
                {
                    return Json(new { success = false, message = error });
                }

                if (!service.HasCompanyContext())
                {
                    return Json(new { success = false, message = "Open a company before running a custom report." });
                }

                if (page < 1)
                {
                    page = 1;
                }

                if (page > 20)
                {
                    page = 20;
                }

                var pageResult = service.Run(validated, page);
                if (!string.IsNullOrEmpty(pageResult.EmptyMessage) && pageResult.Rows.Count == 0)
                {
                    return Json(new { success = false, message = pageResult.EmptyMessage });
                }

                return Json(new
                {
                    success = true,
                    headers = pageResult.Headers.Select(HttpUtility.HtmlEncode).ToList(),
                    rows = pageResult.Rows.Select(row => row.Select(HttpUtility.HtmlEncode).ToList()).ToList(),
                    hasMore = pageResult.HasMore,
                    page = pageResult.Page,
                    warning = pageResult.Warning
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCustom(int? id, string name, string description, string configJson)
        {
            using (var service = new CustomReportService())
            {
                ValidatedCustomReport validated;
                string error;
                if (!CustomReportConfigValidator.TryValidate(
                    name,
                    description,
                    configJson,
                    service.PositionIdsBelongToCompany,
                    service.DepartmentIdsBelongToCompany,
                    out validated,
                    out error))
                {
                    var model = BuildBuilderViewModelFromPost(service, id, name, description, configJson);
                    model.Error = error;
                    return View("~/Views/Reports/Builder.cshtml", model);
                }

                var savedId = service.Save(id, validated, User.Identity.Name, out error);
                if (savedId <= 0)
                {
                    var model = BuildBuilderViewModelFromValidated(service, validated, id);
                    model.Error = error;
                    return View("~/Views/Reports/Builder.cshtml", model);
                }

                return RedirectToAction("Edit", new { id = savedId, saved = 1 });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCustom(int id)
        {
            using (var service = new CustomReportService())
            {
                if (!service.Delete(id))
                {
                    return HttpNotFound();
                }

                return RedirectToAction("Index");
            }
        }

        public ActionResult Run(int id)
        {
            using (var service = new CustomReportService())
            {
                var model = BuildRunViewModel(service, id, 1);
                if (model == null)
                {
                    return HttpNotFound();
                }

                return View("~/Views/Reports/Run.cshtml", model);
            }
        }

        public ActionResult RunData(int id, int page)
        {
            using (var service = new CustomReportService())
            {
                var definition = service.GetForCurrentCompany(id);
                if (definition == null)
                {
                    return HttpNotFound();
                }

                ValidatedCustomReport validated;
                string error;
                if (!CustomReportConfigValidator.TryValidate(
                    definition.Name,
                    definition.Description,
                    definition.ConfigJson,
                    service.PositionIdsBelongToCompany,
                    service.DepartmentIdsBelongToCompany,
                    out validated,
                    out error))
                {
                    return Json(new { success = false, message = error }, JsonRequestBehavior.AllowGet);
                }

                if (page < 1)
                {
                    page = 1;
                }

                if (page > 20)
                {
                    page = 20;
                }

                var pageResult = service.Run(validated, page);
                if (!string.IsNullOrEmpty(pageResult.EmptyMessage) && pageResult.Rows.Count == 0)
                {
                    return Json(new { success = false, message = pageResult.EmptyMessage }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    headers = pageResult.Headers.Select(HttpUtility.HtmlEncode).ToList(),
                    rows = pageResult.Rows.Select(row => row.Select(HttpUtility.HtmlEncode).ToList()).ToList(),
                    hasMore = pageResult.HasMore,
                    page = pageResult.Page,
                    warning = pageResult.Warning
                }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult ExportSaved(int id)
        {
            using (var service = new CustomReportService())
            {
                var definition = service.GetForCurrentCompany(id);
                if (definition == null)
                {
                    return HttpNotFound();
                }

                return ExportValidated(service, definition.Name, definition.Description, definition.ConfigJson, id);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportCustom(string name, string description, string configJson)
        {
            using (var service = new CustomReportService())
            {
                return ExportValidated(service, name, description, configJson, null);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateDirect(string reportType, string format = "csv")
        {
            try
            {
                if (string.IsNullOrEmpty(reportType))
                {
                    return Json(new { success = false, message = "Please select a report type" });
                }

                var filePath = _reportService.GenerateReportByType(reportType, User.Identity.Name, format);
                var fileName = System.IO.Path.GetFileName(filePath);

                return Json(new
                {
                    success = true,
                    message = string.Format("Report '{0}' generated successfully", fileName),
                    fileName = fileName,
                    filePath = filePath
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error generating report: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Preview(string reportType)
        {
            try
            {
                if (string.IsNullOrEmpty(reportType))
                {
                    return Json(new { success = false, message = "Please select a report type" });
                }

                var html = _reportService.PreviewReportByType(reportType, User.Identity.Name);

                return Json(new
                {
                    success = true,
                    html = html
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error generating preview: " + ex.Message });
            }
        }

        public ActionResult Download(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return HttpNotFound();
                }

                var filePath = System.IO.Path.Combine(Server.MapPath("~/Reports"), fileName);
                if (!System.IO.File.Exists(filePath))
                {
                    return HttpNotFound();
                }

                string extension = System.IO.Path.GetExtension(fileName).ToLower();
                string contentType = "application/octet-stream";

                if (extension == ".csv") contentType = "text/csv";
                else if (extension == ".pdf") contentType = "application/pdf";
                else if (extension == ".html") contentType = "text/html";

                return File(filePath, contentType, fileName);
            }
            catch
            {
                return HttpNotFound();
            }
        }

        private static ActionResult ExportValidated(
            CustomReportService service,
            string name,
            string description,
            string configJson,
            int? savedId)
        {
            ValidatedCustomReport validated;
            string error;
            if (!CustomReportConfigValidator.TryValidate(
                name,
                description,
                configJson,
                service.PositionIdsBelongToCompany,
                service.DepartmentIdsBelongToCompany,
                out validated,
                out error))
            {
                return new HttpStatusCodeResult(400, error);
            }

            var bytes = service.Export(validated, out error);
            if (bytes == null)
            {
                return new HttpStatusCodeResult(400, error);
            }

            var fileName = "custom-report-" +
                (savedId.HasValue ? savedId.Value.ToString() : "draft") + "-" +
                DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".csv";

            return new FileContentResult(bytes, "text/csv") { FileDownloadName = fileName };
        }

        private static CustomReportBuilderViewModel BuildEmptyBuilderViewModel(CustomReportService service)
        {
            var positions = service.GetInitialPositions();
            var departments = service.GetInitialDepartments();

            return new CustomReportBuilderViewModel
            {
                DatasetWireKey = CustomReportCatalog.ApplicationsWireKey,
                SelectedColumns = CustomReportCatalog.ApplicationDefaultColumns.ToList(),
                Preset = "allTime",
                SelectedStatuses = new List<string>(),
                SelectedPositionIds = new List<int>(),
                SelectedDepartmentIds = new List<int>(),
                SortColumn = "appliedOn",
                SortDirection = "desc",
                Datasets = CustomReportCatalog.GetAllDatasets(),
                Statuses = CustomReportCatalog.Statuses,
                Positions = MapFilterOptions(positions),
                PositionsHasMore = positions.HasMore,
                Departments = MapFilterOptions(departments),
                DepartmentsHasMore = departments.HasMore,
                CompanyWarning = service.HasCompanyContext()
                    ? null
                    : "Open a company before running a custom report."
            };
        }

        private static CustomReportBuilderViewModel BuildBuilderViewModelFromValidated(
            CustomReportService service,
            ValidatedCustomReport validated,
            int? id)
        {
            var datasetDefinition = CustomReportCatalog.Get(validated.Dataset);
            var positions = service.GetInitialPositions();
            var departments = service.GetInitialDepartments();

            return new CustomReportBuilderViewModel
            {
                Id = id,
                Name = validated.Name,
                Description = validated.Description,
                DatasetWireKey = datasetDefinition.WireKey,
                SelectedColumns = validated.Columns.ToList(),
                Preset = CustomReportCatalog.GetPresetWireKey(validated.Preset),
                From = validated.Preset == CustomReportTimePreset.Custom && validated.FromUtcInclusive.HasValue
                    ? validated.FromUtcInclusive.Value.ToString("yyyy-MM-dd")
                    : null,
                To = validated.Preset == CustomReportTimePreset.Custom && validated.ToUtcExclusive.HasValue
                    ? validated.ToUtcExclusive.Value.AddDays(-1).ToString("yyyy-MM-dd")
                    : null,
                SelectedStatuses = validated.Statuses != null ? validated.Statuses.ToList() : new List<string>(),
                SelectedPositionIds = validated.PositionIds != null ? validated.PositionIds.ToList() : new List<int>(),
                SelectedDepartmentIds = validated.DepartmentIds != null ? validated.DepartmentIds.ToList() : new List<int>(),
                OpenPositionsOnly = validated.OpenPositionsOnly,
                SortColumn = validated.SortColumn,
                SortDirection = CustomReportCatalog.GetSortDirectionWireKey(validated.SortDirection),
                Datasets = CustomReportCatalog.GetAllDatasets(),
                Statuses = CustomReportCatalog.Statuses,
                Positions = MapFilterOptions(positions),
                PositionsHasMore = positions.HasMore,
                Departments = MapFilterOptions(departments),
                DepartmentsHasMore = departments.HasMore,
                CompanyWarning = service.HasCompanyContext()
                    ? null
                    : "Open a company before running a custom report."
            };
        }

        private static CustomReportBuilderViewModel BuildBuilderViewModelFromPost(
            CustomReportService service,
            int? id,
            string name,
            string description,
            string configJson)
        {
            var model = BuildEmptyBuilderViewModel(service);
            model.Id = id;
            model.Name = name;
            model.Description = description;

            if (!string.IsNullOrEmpty(configJson))
            {
                try
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<CustomReportConfig>(configJson);
                    if (parsed != null)
                    {
                        model.DatasetWireKey = parsed.Dataset ?? model.DatasetWireKey;
                        model.SelectedColumns = parsed.Columns ?? model.SelectedColumns;
                        if (parsed.TimeFrame != null)
                        {
                            model.Preset = parsed.TimeFrame.Preset ?? model.Preset;
                            model.From = parsed.TimeFrame.From;
                            model.To = parsed.TimeFrame.To;
                        }

                        if (parsed.Filters != null)
                        {
                            model.SelectedStatuses = parsed.Filters.Statuses ?? model.SelectedStatuses;
                            model.SelectedPositionIds = parsed.Filters.PositionIds ?? model.SelectedPositionIds;
                            model.SelectedDepartmentIds = parsed.Filters.DepartmentIds ?? model.SelectedDepartmentIds;
                            model.OpenPositionsOnly = parsed.Filters.OpenPositionsOnly;
                        }

                        if (parsed.Sort != null)
                        {
                            model.SortColumn = parsed.Sort.Column ?? model.SortColumn;
                            model.SortDirection = parsed.Sort.Direction ?? model.SortDirection;
                        }
                    }
                }
                catch
                {
                    // Keep defaults; validation error is shown separately.
                }
            }

            return model;
        }

        private static CustomReportRunViewModel BuildRunViewModel(CustomReportService service, int id, int page)
        {
            var definition = service.GetForCurrentCompany(id);
            if (definition == null)
            {
                return null;
            }

            ValidatedCustomReport validated;
            string error;
            if (!CustomReportConfigValidator.TryValidate(
                definition.Name,
                definition.Description,
                definition.ConfigJson,
                service.PositionIdsBelongToCompany,
                service.DepartmentIdsBelongToCompany,
                out validated,
                out error))
            {
                CustomReportDataset dataset;
                var label = CustomReportCatalog.ParseDataset(definition.DatasetKey, out dataset)
                    ? CustomReportCatalog.Get(dataset).Label
                    : definition.DatasetKey;

                return new CustomReportRunViewModel
                {
                    Id = id,
                    Name = definition.Name,
                    Description = definition.Description,
                    DatasetLabel = label,
                    Error = error
                };
            }

            CustomReportDataset datasetEnum;
            var datasetLabel = CustomReportCatalog.ParseDataset(definition.DatasetKey, out datasetEnum)
                ? CustomReportCatalog.Get(datasetEnum).Label
                : definition.DatasetKey;

            return new CustomReportRunViewModel
            {
                Id = id,
                Name = definition.Name,
                Description = definition.Description,
                DatasetLabel = datasetLabel,
                Page = service.Run(validated, page)
            };
        }

        private static IList<IdLabel> MapFilterOptions(FilterOptionsPage page)
        {
            return page.Items.Select(i => new IdLabel { Id = i.Id, Label = i.Label }).ToList();
        }
    }
}
