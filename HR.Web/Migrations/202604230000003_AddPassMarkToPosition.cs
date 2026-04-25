namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddPassMarkToPosition : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Positions", "PassMark", c => c.Decimal(nullable: false, precision: 5, scale: 2, defaultValue: 50m));
        }

        public override void Down()
        {
            DropColumn("dbo.Positions", "PassMark");
        }
    }
}
