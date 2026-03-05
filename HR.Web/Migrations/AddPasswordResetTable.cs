using System;
using System.Data.Entity.Migrations;
using HR.Web.Models;

namespace HR.Web.Migrations
{
    public partial class AddPasswordResetTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PasswordResets",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        Token = c.String(nullable: false, maxLength: 255),
                        ExpiryDate = c.DateTime(nullable: false),
                        IsUsed = c.Boolean(nullable: false),
                        CreatedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PasswordResets", "UserId", "dbo.Users");
            DropIndex("dbo.PasswordResets", new[] { "UserId" });
            DropTable("dbo.PasswordResets");
        }
    }
}
