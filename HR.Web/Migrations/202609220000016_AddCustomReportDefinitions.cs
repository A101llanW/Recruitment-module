namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddCustomReportDefinitions : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.CustomReportDefinitions",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    CompanyId = c.Int(nullable: false),
                    Name = c.String(nullable: false, maxLength: 100),
                    Description = c.String(maxLength: 500),
                    DatasetKey = c.String(nullable: false, maxLength: 50),
                    ConfigJson = c.String(nullable: false),
                    CreatedBy = c.String(nullable: false, maxLength: 100),
                    CreatedOn = c.DateTime(nullable: false),
                    UpdatedBy = c.String(maxLength: 100),
                    UpdatedOn = c.DateTime(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Companies", t => t.CompanyId)
                .Index(t => t.CompanyId, name: "IX_CustomReportDefinitions_CompanyId_CreatedOn");
        }

        public override void Down()
        {
            DropForeignKey("dbo.CustomReportDefinitions", "FK_CustomReportDefinitions_Companies", "dbo.Companies");
            DropIndex("dbo.CustomReportDefinitions", "IX_CustomReportDefinitions_CompanyId_CreatedOn");
            DropTable("dbo.CustomReportDefinitions");
        }
    }
}
