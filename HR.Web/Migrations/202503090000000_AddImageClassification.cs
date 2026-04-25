namespace HR.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddImageClassification : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ImageClassifications",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        OriginalFileName = c.String(nullable: false, maxLength: 255),
                        SavedFileName = c.String(nullable: false, maxLength: 255),
                        ImagePath = c.String(nullable: false, maxLength: 500),
                        Description = c.String(maxLength: 1000),
                        UploadedAt = c.DateTime(nullable: false),
                        ProcessedAt = c.DateTime(nullable: false),
                        Success = c.Boolean(nullable: false),
                        ErrorMessage = c.String(maxLength: 1000),
                        UploadedByUserId = c.Int(),
                        TenantId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.UploadedByUserId)
                .ForeignKey("dbo.Companies", t => t.TenantId, cascadeDelete: false)
                .Index(t => t.UploadedByUserId)
                .Index(t => t.TenantId);
            
            CreateTable(
                "dbo.ImageDetections",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ObjectType = c.String(nullable: false, maxLength: 50),
                        Confidence = c.Decimal(nullable: false, precision: 18, scale: 4),
                        BoundingBoxX = c.Int(nullable: false),
                        BoundingBoxY = c.Int(nullable: false),
                        BoundingBoxWidth = c.Int(nullable: false),
                        BoundingBoxHeight = c.Int(nullable: false),
                        DetectedAt = c.DateTime(nullable: false),
                        ImageClassificationId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ImageClassifications", t => t.ImageClassificationId, cascadeDelete: false)
                .Index(t => t.ImageClassificationId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ImageDetections", "ImageClassificationId", "dbo.ImageClassifications");
            DropForeignKey("dbo.ImageClassifications", "TenantId", "dbo.Companies");
            DropForeignKey("dbo.ImageClassifications", "UploadedByUserId", "dbo.Users");
            DropIndex("dbo.ImageDetections", new[] { "ImageClassificationId" });
            DropIndex("dbo.ImageClassifications", new[] { "TenantId" });
            DropIndex("dbo.ImageClassifications", new[] { "UploadedByUserId" });
            DropTable("dbo.ImageDetections");
            DropTable("dbo.ImageClassifications");
        }
    }
}
