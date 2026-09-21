namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddApplicationNotifyRecipients : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.CompanyApplicationNotifyRecipients",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    CompanyId = c.Int(nullable: false),
                    Email = c.String(nullable: false, maxLength: 255),
                    Label = c.String(maxLength: 150),
                    AccessMode = c.String(nullable: false, maxLength: 30),
                    SortOrder = c.Int(nullable: false),
                    IsActive = c.Boolean(nullable: false),
                    CreatedDate = c.DateTime(nullable: false, precision: 0, storeType: "datetime2"),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Companies", t => t.CompanyId, cascadeDelete: true)
                .Index(t => t.CompanyId);

            CreateTable(
                "dbo.ApplicationNotificationAccessTokens",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ApplicationId = c.Int(nullable: false),
                    RecipientId = c.Int(nullable: false),
                    Token = c.String(nullable: false, maxLength: 64),
                    ExpiresAt = c.DateTime(nullable: false, precision: 0, storeType: "datetime2"),
                    CreatedDate = c.DateTime(nullable: false, precision: 0, storeType: "datetime2"),
                    RevokedAt = c.DateTime(precision: 0, storeType: "datetime2"),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Applications", t => t.ApplicationId, cascadeDelete: true)
                .ForeignKey("dbo.CompanyApplicationNotifyRecipients", t => t.RecipientId)
                .Index(t => t.ApplicationId)
                .Index(t => t.RecipientId)
                .Index(t => t.Token, unique: true);
        }

        public override void Down()
        {
            DropForeignKey("dbo.ApplicationNotificationAccessTokens", "RecipientId", "dbo.CompanyApplicationNotifyRecipients");
            DropForeignKey("dbo.ApplicationNotificationAccessTokens", "ApplicationId", "dbo.Applications");
            DropForeignKey("dbo.CompanyApplicationNotifyRecipients", "CompanyId", "dbo.Companies");
            DropIndex("dbo.ApplicationNotificationAccessTokens", new[] { "Token" });
            DropIndex("dbo.ApplicationNotificationAccessTokens", new[] { "RecipientId" });
            DropIndex("dbo.ApplicationNotificationAccessTokens", new[] { "ApplicationId" });
            DropIndex("dbo.CompanyApplicationNotifyRecipients", new[] { "CompanyId" });
            DropTable("dbo.ApplicationNotificationAccessTokens");
            DropTable("dbo.CompanyApplicationNotifyRecipients");
        }
    }
}
