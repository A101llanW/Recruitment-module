namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddCompanyLogoPathToCompanies : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Companies", "LogoPath", c => c.String(maxLength: 260));
        }

        public override void Down()
        {
            DropColumn("dbo.Companies", "LogoPath");
        }
    }
}
