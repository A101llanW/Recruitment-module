namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddPositionExpiryDate : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Positions", "ExpiryDate", c => c.DateTime());
        }

        public override void Down()
        {
            DropColumn("dbo.Positions", "ExpiryDate");
        }
    }
}
