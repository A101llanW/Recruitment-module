using System.Data.Entity.Migrations;

namespace HR.Web.Migrations
{
    public partial class AddPasswordHashToUser : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "PasswordHash", c => c.String(nullable: false, maxLength: 256));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "PasswordHash");
        }
    }
}
