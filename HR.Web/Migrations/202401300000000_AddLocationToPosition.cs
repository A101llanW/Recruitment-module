namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddLocationToPosition : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Positions", "Location", c => c.String(maxLength: 200));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Positions", "Location");
        }
    }
}
