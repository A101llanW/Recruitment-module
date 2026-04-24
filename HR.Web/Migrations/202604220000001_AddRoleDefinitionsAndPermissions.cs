namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddRoleDefinitionsAndPermissions : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.RoleDefinitions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        CompanyId = c.Int(),
                        Name = c.String(nullable: false, maxLength: 100),
                        Description = c.String(maxLength: 500),
                        CreatedByUserName = c.String(nullable: false, maxLength: 100),
                        CreatedDate = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Companies", t => t.CompanyId)
                .Index(t => t.CompanyId)
                .Index(t => t.Name);

            CreateTable(
                "dbo.RolePermissions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        RoleDefinitionId = c.Int(nullable: false),
                        ModuleKey = c.String(nullable: false, maxLength: 50),
                        AccessLevel = c.String(nullable: false, maxLength: 20),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.RoleDefinitions", t => t.RoleDefinitionId)
                .Index(t => new { t.RoleDefinitionId, t.ModuleKey }, unique: true, name: "IX_RolePermission_Module");

            AddColumn("dbo.Users", "RoleDefinitionId", c => c.Int());
            CreateIndex("dbo.Users", "RoleDefinitionId");
            AddForeignKey("dbo.Users", "RoleDefinitionId", "dbo.RoleDefinitions", "Id");
        }

        public override void Down()
        {
            DropForeignKey("dbo.Users", "RoleDefinitionId", "dbo.RoleDefinitions");
            DropForeignKey("dbo.RolePermissions", "RoleDefinitionId", "dbo.RoleDefinitions");
            DropForeignKey("dbo.RoleDefinitions", "CompanyId", "dbo.Companies");
            DropIndex("dbo.Users", new[] { "RoleDefinitionId" });
            DropIndex("dbo.RolePermissions", "IX_RolePermission_Module");
            DropIndex("dbo.RoleDefinitions", new[] { "Name" });
            DropIndex("dbo.RoleDefinitions", new[] { "CompanyId" });
            DropColumn("dbo.Users", "RoleDefinitionId");
            DropTable("dbo.RolePermissions");
            DropTable("dbo.RoleDefinitions");
        }
    }
}
