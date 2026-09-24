using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Services
{
    public class CustomReportService : IDisposable
    {
        private const int PageSize = 10;
        private const int MaxPreviewPage = 20;
        private const int ExportRowCap = 5000;

        private static int GetRowCap(ValidatedCustomReport config)
        {
            if (config == null || config.RowLimit <= 0)
            {
                return CustomReportConfigValidator.DefaultRowLimit;
            }

            return Math.Min(config.RowLimit, ExportRowCap);
        }

        private readonly UnitOfWork _uow;
        private readonly TenantService _tenantService;

        public CustomReportService()
        {
            _uow = new UnitOfWork();
            _tenantService = new TenantService(_uow);
        }

        public IQueryable<T> FilterToCurrentCompany<T>(IQueryable<T> query) where T : class, ITenantEntity
        {
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (!companyId.HasValue)
            {
                return query.Where(e => false);
            }

            return query.Where(e => e.CompanyId == companyId.Value);
        }

        public bool HasCompanyContext()
        {
            return _tenantService.GetCurrentUserCompanyId().HasValue;
        }

        public CustomReportDefinition GetForCurrentCompany(int id)
        {
            var companyId = _tenantService.GetCurrentUserCompanyId();
            if (!companyId.HasValue)
            {
                return null;
            }

            var definition = _uow.CustomReportDefinitions.Get(id);
            if (definition == null || definition.CompanyId != companyId.Value)
            {
                return null;
            }

            return definition;
        }

        public CustomReportListPage List(int page)
        {
            if (page < 1)
            {
                page = 1;
            }

            var query = FilterToCurrentCompany(_uow.CustomReportDefinitions.GetAll().AsQueryable())
                .OrderByDescending(d => d.UpdatedOn ?? d.CreatedOn)
                .ThenByDescending(d => d.Id);

            var skip = (page - 1) * PageSize;
            var items = query.Skip(skip).Take(PageSize + 1).ToList();
            var hasMore = items.Count > PageSize;
            if (hasMore)
            {
                items = items.Take(PageSize).ToList();
            }

            return new CustomReportListPage
            {
                Page = page,
                HasMore = hasMore,
                Items = items.Select(d =>
                {
                    CustomReportDataset dataset;
                    var label = CustomReportCatalog.ParseDataset(d.DatasetKey, out dataset)
                        ? CustomReportCatalog.Get(dataset).Label
                        : d.DatasetKey;
                    var displayDate = (d.UpdatedOn ?? d.CreatedOn).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    return new CustomReportListItem
                    {
                        Id = d.Id,
                        Name = d.Name,
                        DatasetLabel = label,
                        UpdatedOnDisplay = displayDate,
                        CreatedBy = d.CreatedBy
                    };
                }).ToList()
            };
        }

        public CustomReportPage Run(ValidatedCustomReport config, int page)
        {
            if (!HasCompanyContext())
            {
                return EmptyPage(page, "Open a company before running a custom report.");
            }

            if (page < 1)
            {
                page = 1;
            }

            if (page > MaxPreviewPage)
            {
                page = MaxPreviewPage;
            }

            switch (config.Dataset)
            {
                case CustomReportDataset.Applications:
                    return RunApplications(config, page, false);
                case CustomReportDataset.ApplicantsPerPosition:
                    return RunApplicantsPerPosition(config, page, false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(config.Dataset), config.Dataset, "Unknown dataset.");
            }
        }

        public byte[] Export(ValidatedCustomReport config, out string error)
        {
            error = null;

            if (!HasCompanyContext())
            {
                error = "Open a company before running a custom report.";
                return null;
            }

            IList<string> headers;
            IList<IList<string>> rows;
            if (!TryBuildExportRows(config, out headers, out rows, out error))
            {
                return null;
            }

            var csv = BuildCsv(headers, rows);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return bytes;
        }

        public int Save(int? id, ValidatedCustomReport config, string userName, out string error)
        {
            error = null;

            if (!HasCompanyContext())
            {
                error = "Open a company before running a custom report.";
                return 0;
            }

            var companyId = _tenantService.GetCurrentUserCompanyId().Value;
            var actor = TruncateUserName(userName);
            var datasetDefinition = CustomReportCatalog.Get(config.Dataset);

            CustomReportDefinition entity;
            if (id.HasValue)
            {
                entity = GetForCurrentCompany(id.Value);
                if (entity == null)
                {
                    error = "Report not found.";
                    return 0;
                }

                entity.Name = config.Name;
                entity.Description = config.Description;
                entity.DatasetKey = datasetDefinition.WireKey;
                entity.ConfigJson = config.ConfigJson;
                entity.UpdatedBy = actor;
                entity.UpdatedOn = DateTime.UtcNow;
                _uow.CustomReportDefinitions.Update(entity);
            }
            else
            {
                entity = new CustomReportDefinition
                {
                    CompanyId = companyId,
                    Name = config.Name,
                    Description = config.Description,
                    DatasetKey = datasetDefinition.WireKey,
                    ConfigJson = config.ConfigJson,
                    CreatedBy = actor,
                    CreatedOn = DateTime.UtcNow
                };
                _uow.CustomReportDefinitions.Add(entity);
            }

            _uow.Complete();
            return entity.Id;
        }

        public bool Delete(int id)
        {
            var entity = GetForCurrentCompany(id);
            if (entity == null)
            {
                return false;
            }

            _uow.CustomReportDefinitions.Remove(entity);
            _uow.Complete();
            return true;
        }

        public bool PositionIdsBelongToCompany(IList<int> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return true;
            }

            if (!HasCompanyContext())
            {
                return false;
            }

            var distinctCount = ids.Distinct().Count();
            var matchCount = FilterToCurrentCompany(_uow.Positions.GetAll().AsQueryable())
                .Where(p => ids.Contains(p.Id))
                .Select(p => p.Id)
                .Distinct()
                .Count();

            return matchCount == distinctCount;
        }

        public bool DepartmentIdsBelongToCompany(IList<int> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return true;
            }

            if (!HasCompanyContext())
            {
                return false;
            }

            var distinctCount = ids.Distinct().Count();
            var matchCount = FilterToCurrentCompany(_uow.Departments.GetAll().AsQueryable())
                .Where(d => ids.Contains(d.Id))
                .Select(d => d.Id)
                .Distinct()
                .Count();

            return matchCount == distinctCount;
        }

        public FilterOptionsPage GetFilterOptions(string kind, int page)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (!HasCompanyContext())
            {
                return new FilterOptionsPage { Page = page, HasMore = false, Items = new List<FilterOptionItem>() };
            }

            if (string.Equals(kind, "positions", StringComparison.OrdinalIgnoreCase))
            {
                return GetPositionOptions(page);
            }

            if (string.Equals(kind, "departments", StringComparison.OrdinalIgnoreCase))
            {
                return GetDepartmentOptions(page);
            }

            return new FilterOptionsPage { Page = page, HasMore = false, Items = new List<FilterOptionItem>() };
        }

        public FilterOptionsPage GetInitialPositions()
        {
            return GetFilterOptions("positions", 1);
        }

        public FilterOptionsPage GetInitialDepartments()
        {
            return GetFilterOptions("departments", 1);
        }

        public void Dispose()
        {
            _uow.Dispose();
        }

        private FilterOptionsPage GetPositionOptions(int page)
        {
            var query = FilterToCurrentCompany(_uow.Positions.GetAll().AsQueryable())
                .OrderBy(p => p.Title)
                .ThenBy(p => p.Id);

            var skip = (page - 1) * PageSize;
            var items = query.Skip(skip).Take(PageSize + 1).ToList();
            var hasMore = items.Count > PageSize;
            if (hasMore)
            {
                items = items.Take(PageSize).ToList();
            }

            return new FilterOptionsPage
            {
                Page = page,
                HasMore = hasMore,
                Items = items.Select(p => new FilterOptionItem { Id = p.Id, Label = p.Title ?? string.Empty }).ToList()
            };
        }

        private FilterOptionsPage GetDepartmentOptions(int page)
        {
            var query = FilterToCurrentCompany(_uow.Departments.GetAll().AsQueryable())
                .OrderBy(d => d.Name)
                .ThenBy(d => d.Id);

            var skip = (page - 1) * PageSize;
            var items = query.Skip(skip).Take(PageSize + 1).ToList();
            var hasMore = items.Count > PageSize;
            if (hasMore)
            {
                items = items.Take(PageSize).ToList();
            }

            return new FilterOptionsPage
            {
                Page = page,
                HasMore = hasMore,
                Items = items.Select(d => new FilterOptionItem { Id = d.Id, Label = d.Name ?? string.Empty }).ToList()
            };
        }

        private CustomReportPage RunApplications(ValidatedCustomReport config, int page, bool forExport)
        {
            var rowCap = GetRowCap(config);
            var headers = config.Columns
                .Select(c => CustomReportCatalog.GetColumnHeader(CustomReportDataset.Applications, c))
                .ToList();

            var query = ApplySharedFilters(
                FilterToCurrentCompany(
                    _uow.Applications.GetAll(a => a.Applicant, a => a.Position, a => a.Position.Department).AsQueryable()),
                config);

            query = ApplyApplicationsSort(query, config);

            if (forExport)
            {
                var exportRows = query.Take(rowCap + 1).ToList();
                if (exportRows.Count > rowCap)
                {
                    return new CustomReportPage
                    {
                        Headers = headers,
                        Rows = new List<IList<string>>(),
                        Page = 1,
                        PageSize = PageSize,
                        HasMore = false,
                        EmptyMessage = string.Format("Narrow the filters or increase the row limit. Exports are limited to {0} rows.", rowCap)
                    };
                }

                var rows = exportRows.Select(a => ProjectApplicationRow(a, config.Columns)).ToList();
                return new CustomReportPage
                {
                    Headers = headers,
                    Rows = rows,
                    Page = 1,
                    PageSize = PageSize,
                    HasMore = false
                };
            }

            var skip = (page - 1) * PageSize;
            var cappedQuery = query.Take(rowCap);
            var pageItems = cappedQuery.Skip(skip).Take(PageSize + 1).ToList();
            var maxPreviewPage = Math.Max(1, (int)Math.Ceiling(rowCap / (double)PageSize));
            var hasMore = pageItems.Count > PageSize && page < maxPreviewPage && page < MaxPreviewPage;
            if (pageItems.Count > PageSize)
            {
                pageItems = pageItems.Take(PageSize).ToList();
            }

            return new CustomReportPage
            {
                Headers = headers,
                Rows = pageItems.Select(a => ProjectApplicationRow(a, config.Columns)).ToList(),
                Page = page,
                PageSize = PageSize,
                HasMore = hasMore,
                EmptyMessage = pageItems.Count == 0 ? "No matching applications." : null
            };
        }

        private CustomReportPage RunApplicantsPerPosition(ValidatedCustomReport config, int page, bool forExport)
        {
            var rowCap = GetRowCap(config);
            var headers = config.Columns
                .Select(c => CustomReportCatalog.GetColumnHeader(CustomReportDataset.ApplicantsPerPosition, c))
                .ToList();

            var query = ApplySharedFilters(
                FilterToCurrentCompany(
                    _uow.Applications.GetAll(a => a.Applicant, a => a.Position, a => a.Position.Department).AsQueryable()),
                config);

            string warning = null;
            var projected = query.Select(a => new AggregateProjection
            {
                ApplicantId = a.ApplicantId,
                PositionId = a.PositionId,
                Title = a.Position.Title,
                Department = a.Position.Department.Name,
                Location = a.Position.Location,
                IsOpen = a.Position.IsOpen
            }).Take(rowCap + 1).ToList();

            if (forExport && projected.Count > rowCap)
            {
                return new CustomReportPage
                {
                    Headers = headers,
                    Rows = new List<IList<string>>(),
                    Page = 1,
                    PageSize = PageSize,
                    HasMore = false,
                    EmptyMessage = string.Format("Narrow the filters or increase the row limit. Exports are limited to {0} rows.", rowCap)
                };
            }

            if (!forExport && projected.Count > rowCap)
            {
                projected = projected.Take(rowCap).ToList();
                warning = string.Format("Counts use the first {0} matching applications. Narrow the filters or raise the row limit for an exact count.", rowCap);
            }

            var grouped = projected
                .GroupBy(x => x.PositionId)
                .Select(g =>
                {
                    var first = g.First();
                    return new AggregateRow
                    {
                        PositionId = g.Key,
                        PositionTitle = first.Title,
                        DepartmentName = first.Department,
                        PositionLocation = first.Location,
                        IsOpen = first.IsOpen,
                        ApplicantCount = g.Select(x => x.ApplicantId).Distinct().Count()
                    };
                })
                .ToList();

            grouped = ApplyAggregateSort(grouped, config).Take(rowCap).ToList();

            if (forExport)
            {
                return new CustomReportPage
                {
                    Headers = headers,
                    Rows = grouped.Select(r => ProjectAggregateRow(r, config.Columns)).ToList(),
                    Page = 1,
                    PageSize = PageSize,
                    HasMore = false,
                    Warning = warning
                };
            }

            var skip = (page - 1) * PageSize;
            var pageItems = grouped.Skip(skip).Take(PageSize + 1).ToList();
            var maxPreviewPage = Math.Max(1, (int)Math.Ceiling(Math.Min(grouped.Count, rowCap) / (double)PageSize));
            var hasMore = pageItems.Count > PageSize && page < maxPreviewPage && page < MaxPreviewPage;
            if (pageItems.Count > PageSize)
            {
                pageItems = pageItems.Take(PageSize).ToList();
            }

            return new CustomReportPage
            {
                Headers = headers,
                Rows = pageItems.Select(r => ProjectAggregateRow(r, config.Columns)).ToList(),
                Page = page,
                PageSize = PageSize,
                HasMore = hasMore,
                Warning = warning,
                EmptyMessage = pageItems.Count == 0 ? "No matching positions." : null
            };
        }

        private bool TryBuildExportRows(ValidatedCustomReport config, out IList<string> headers, out IList<IList<string>> rows, out string error)
        {
            headers = new List<string>();
            rows = new List<IList<string>>();
            error = null;

            CustomReportPage pageResult;
            switch (config.Dataset)
            {
                case CustomReportDataset.Applications:
                    pageResult = RunApplications(config, 1, true);
                    break;
                case CustomReportDataset.ApplicantsPerPosition:
                    pageResult = RunApplicantsPerPosition(config, 1, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(config.Dataset), config.Dataset, "Unknown dataset.");
            }

            if (!string.IsNullOrEmpty(pageResult.EmptyMessage) &&
                string.Equals(pageResult.EmptyMessage, "Narrow the filters. Exports are limited to 5000 rows.", StringComparison.Ordinal))
            {
                error = pageResult.EmptyMessage;
                return false;
            }

            headers = pageResult.Headers;
            rows = pageResult.Rows;
            return true;
        }

        private static IQueryable<Application> ApplySharedFilters(IQueryable<Application> query, ValidatedCustomReport config)
        {
            if (config.Statuses != null && config.Statuses.Count > 0)
            {
                query = query.Where(a => config.Statuses.Contains(a.Status));
            }

            if (config.PositionIds != null && config.PositionIds.Count > 0)
            {
                query = query.Where(a => config.PositionIds.Contains(a.PositionId));
            }

            if (config.DepartmentIds != null && config.DepartmentIds.Count > 0)
            {
                query = query.Where(a => config.DepartmentIds.Contains(a.Position.DepartmentId));
            }

            if (config.OpenPositionsOnly)
            {
                query = query.Where(a => a.Position.IsOpen);
            }

            if (config.FromUtcInclusive.HasValue)
            {
                var from = config.FromUtcInclusive.Value;
                query = query.Where(a => a.AppliedOn >= from);
            }

            if (config.ToUtcExclusive.HasValue)
            {
                var to = config.ToUtcExclusive.Value;
                query = query.Where(a => a.AppliedOn < to);
            }

            return query;
        }

        private static IQueryable<Application> ApplyApplicationsSort(IQueryable<Application> query, ValidatedCustomReport config)
        {
            var descending = config.SortDirection == CustomReportSortDirection.Desc;

            switch (config.SortColumn)
            {
                case "applicantName":
                    query = descending
                        ? query.OrderByDescending(a => a.Applicant.FullName).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.Applicant.FullName).ThenByDescending(a => a.Id);
                    break;
                case "email":
                    query = descending
                        ? query.OrderByDescending(a => a.Applicant.Email).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.Applicant.Email).ThenByDescending(a => a.Id);
                    break;
                case "phone":
                    query = descending
                        ? query.OrderByDescending(a => a.Applicant.Phone).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.Applicant.Phone).ThenByDescending(a => a.Id);
                    break;
                case "positionTitle":
                    query = descending
                        ? query.OrderByDescending(a => a.Position.Title).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.Position.Title).ThenByDescending(a => a.Id);
                    break;
                case "departmentName":
                    query = descending
                        ? query.OrderByDescending(a => a.Position.Department.Name).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.Position.Department.Name).ThenByDescending(a => a.Id);
                    break;
                case "status":
                    query = descending
                        ? query.OrderByDescending(a => a.Status).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.Status).ThenByDescending(a => a.Id);
                    break;
                case "appliedOn":
                    query = descending
                        ? query.OrderByDescending(a => a.AppliedOn).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.AppliedOn).ThenByDescending(a => a.Id);
                    break;
                case "score":
                    query = descending
                        ? query.OrderByDescending(a => a.Score).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.Score).ThenByDescending(a => a.Id);
                    break;
                case "workExperienceLevel":
                    query = descending
                        ? query.OrderByDescending(a => a.WorkExperienceLevel).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.WorkExperienceLevel).ThenByDescending(a => a.Id);
                    break;
                case "positionLocation":
                    query = descending
                        ? query.OrderByDescending(a => a.Position.Location).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.Position.Location).ThenByDescending(a => a.Id);
                    break;
                case "isOpen":
                    query = descending
                        ? query.OrderByDescending(a => a.Position.IsOpen).ThenByDescending(a => a.Id)
                        : query.OrderBy(a => a.Position.IsOpen).ThenByDescending(a => a.Id);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(config.SortColumn), config.SortColumn, "Unknown sort column.");
            }

            return query;
        }

        private static IEnumerable<AggregateRow> ApplyAggregateSort(IList<AggregateRow> rows, ValidatedCustomReport config)
        {
            var descending = config.SortDirection == CustomReportSortDirection.Desc;

            switch (config.SortColumn)
            {
                case "positionTitle":
                    return descending
                        ? rows.OrderByDescending(r => r.PositionTitle).ThenBy(r => r.PositionId)
                        : rows.OrderBy(r => r.PositionTitle).ThenBy(r => r.PositionId);
                case "departmentName":
                    return descending
                        ? rows.OrderByDescending(r => r.DepartmentName).ThenBy(r => r.PositionId)
                        : rows.OrderBy(r => r.DepartmentName).ThenBy(r => r.PositionId);
                case "positionLocation":
                    return descending
                        ? rows.OrderByDescending(r => r.PositionLocation).ThenBy(r => r.PositionId)
                        : rows.OrderBy(r => r.PositionLocation).ThenBy(r => r.PositionId);
                case "isOpen":
                    return descending
                        ? rows.OrderByDescending(r => r.IsOpen).ThenBy(r => r.PositionId)
                        : rows.OrderBy(r => r.IsOpen).ThenBy(r => r.PositionId);
                case "applicantCount":
                    return descending
                        ? rows.OrderByDescending(r => r.ApplicantCount).ThenBy(r => r.PositionId)
                        : rows.OrderBy(r => r.ApplicantCount).ThenBy(r => r.PositionId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(config.SortColumn), config.SortColumn, "Unknown sort column.");
            }
        }

        private static IList<string> ProjectApplicationRow(Application application, IList<string> columns)
        {
            var values = new List<string>();
            foreach (var column in columns)
            {
                switch (column)
                {
                    case "applicantName":
                        values.Add(application.Applicant != null ? application.Applicant.FullName ?? string.Empty : string.Empty);
                        break;
                    case "email":
                        values.Add(application.Applicant != null ? application.Applicant.Email ?? string.Empty : string.Empty);
                        break;
                    case "phone":
                        values.Add(application.Applicant != null ? application.Applicant.Phone ?? string.Empty : string.Empty);
                        break;
                    case "positionTitle":
                        values.Add(application.Position != null ? application.Position.Title ?? string.Empty : string.Empty);
                        break;
                    case "departmentName":
                        values.Add(application.Position != null && application.Position.Department != null
                            ? application.Position.Department.Name ?? string.Empty
                            : string.Empty);
                        break;
                    case "status":
                        values.Add(application.Status ?? string.Empty);
                        break;
                    case "appliedOn":
                        values.Add(application.AppliedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                        break;
                    case "score":
                        values.Add(application.Score.HasValue
                            ? application.Score.Value.ToString("0.##", CultureInfo.InvariantCulture)
                            : string.Empty);
                        break;
                    case "workExperienceLevel":
                        values.Add(application.WorkExperienceLevel ?? string.Empty);
                        break;
                    case "positionLocation":
                        values.Add(application.Position != null ? application.Position.Location ?? string.Empty : string.Empty);
                        break;
                    case "isOpen":
                        values.Add(application.Position != null && application.Position.IsOpen ? "Yes" : "No");
                        break;
                    default:
                        values.Add(string.Empty);
                        break;
                }
            }

            return values;
        }

        private static IList<string> ProjectAggregateRow(AggregateRow row, IList<string> columns)
        {
            var values = new List<string>();
            foreach (var column in columns)
            {
                switch (column)
                {
                    case "positionTitle":
                        values.Add(row.PositionTitle ?? string.Empty);
                        break;
                    case "departmentName":
                        values.Add(row.DepartmentName ?? string.Empty);
                        break;
                    case "positionLocation":
                        values.Add(row.PositionLocation ?? string.Empty);
                        break;
                    case "isOpen":
                        values.Add(row.IsOpen ? "Yes" : "No");
                        break;
                    case "applicantCount":
                        values.Add(row.ApplicantCount.ToString(CultureInfo.InvariantCulture));
                        break;
                    default:
                        values.Add(string.Empty);
                        break;
                }
            }

            return values;
        }

        private static CustomReportPage EmptyPage(int page, string message)
        {
            return new CustomReportPage
            {
                Headers = new List<string>(),
                Rows = new List<IList<string>>(),
                Page = page,
                PageSize = PageSize,
                HasMore = false,
                EmptyMessage = message
            };
        }

        private static string BuildCsv(IList<string> headers, IList<IList<string>> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", headers.Select(CsvCell)));
            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(",", row.Select(CsvCell)));
            }

            return builder.ToString();
        }

        private static string CsvCell(string value)
        {
            var text = value ?? string.Empty;
            if (text.Length > 0)
            {
                var first = text[0];
                if (first == '=' || first == '+' || first == '-' || first == '@' || first == '\t' || first == '\r')
                {
                    text = "'" + text;
                }
            }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static string TruncateUserName(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return string.Empty;
            }

            return userName.Length <= 100 ? userName : userName.Substring(0, 100);
        }

        private sealed class AggregateProjection
        {
            public int ApplicantId { get; set; }
            public int PositionId { get; set; }
            public string Title { get; set; }
            public string Department { get; set; }
            public string Location { get; set; }
            public bool IsOpen { get; set; }
        }

        private sealed class AggregateRow
        {
            public int PositionId { get; set; }
            public string PositionTitle { get; set; }
            public string DepartmentName { get; set; }
            public string PositionLocation { get; set; }
            public bool IsOpen { get; set; }
            public int ApplicantCount { get; set; }
        }
    }

    public sealed class FilterOptionItem
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }

    public sealed class FilterOptionsPage
    {
        public IList<FilterOptionItem> Items { get; set; }
        public int Page { get; set; }
        public bool HasMore { get; set; }
    }
}
