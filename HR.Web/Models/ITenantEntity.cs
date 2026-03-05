namespace HR.Web.Models
{
    public interface ITenantEntity
    {
        int? CompanyId { get; set; }
        Company Company { get; set; }
    }
}
