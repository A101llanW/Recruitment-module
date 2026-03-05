namespace HR.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddReportEntity : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Reports",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Type = c.String(nullable: false, maxLength: 50),
                        Description = c.String(maxLength: 500),
                        CreatedDate = c.DateTime(nullable: false),
                        GeneratedDate = c.DateTime(),
                        GeneratedBy = c.String(),
                        FilePath = c.String(maxLength: 500),
                        IsActive = c.Boolean(nullable: false),
                        Parameters = c.String(),
                    })
                .PrimaryKey(t => t.Id);
        }
        
        public override void Down()
        {
            DropTable("dbo.Reports");
        }
    }
}
