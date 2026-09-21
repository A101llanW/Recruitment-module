namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddCompanySmtpSettings : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.CompanySmtpSettings",
                c => new
                {
                    CompanyId = c.Int(nullable: false),
                    IsEnabled = c.Boolean(nullable: false),
                    SmtpHost = c.String(maxLength: 255),
                    SmtpPort = c.Int(nullable: false),
                    SmtpUser = c.String(maxLength: 255),
                    SmtpPasswordEncrypted = c.String(maxLength: 1024),
                    SmtpEnableSsl = c.Boolean(nullable: false),
                    FromEmail = c.String(maxLength: 255),
                    FromName = c.String(maxLength: 150),
                    UpdatedDate = c.DateTime(nullable: false, precision: 0, storeType: "datetime2"),
                })
                .PrimaryKey(t => t.CompanyId)
                .ForeignKey("dbo.Companies", t => t.CompanyId, cascadeDelete: true)
                .Index(t => t.CompanyId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.CompanySmtpSettings", "CompanyId", "dbo.Companies");
            DropIndex("dbo.CompanySmtpSettings", new[] { "CompanyId" });
            DropTable("dbo.CompanySmtpSettings");
        }
    }
}
