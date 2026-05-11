namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AddIsPanelistToUsers : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "IsPanelist", c => c.Boolean(nullable: false, defaultValue: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "IsPanelist");
        }
    }
}
