using System.Data.Entity.Migrations;

namespace HR.Web.Migrations
{
    public partial class AddSecurityTables : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.LoginAttempts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Username = c.String(nullable: false, maxLength: 100),
                        IPAddress = c.String(nullable: false, maxLength: 45),
                        AttemptTime = c.DateTime(nullable: false),
                        WasSuccessful = c.Boolean(nullable: false),
                        FailureReason = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.AuditLogs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Username = c.String(nullable: false, maxLength: 100),
                        Action = c.String(nullable: false, maxLength: 100),
                        Controller = c.String(nullable: false, maxLength: 100),
                        EntityId = c.String(),
                        OldValues = c.String(),
                        NewValues = c.String(),
                        IPAddress = c.String(nullable: false, maxLength: 45),
                        Timestamp = c.DateTime(nullable: false),
                        UserAgent = c.String(),
                        WasSuccessful = c.Boolean(nullable: false),
                        ErrorMessage = c.String(),
                    })
                .PrimaryKey(t => t.Id);
        }
        
        public override void Down()
        {
            DropTable("dbo.AuditLogs");
            DropTable("dbo.LoginAttempts");
        }
    }
}
