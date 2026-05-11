namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddHasSecondaryStageToPosition : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Positions", "HasSecondaryStage", c => c.Boolean(nullable: false, defaultValue: false));
        }

        public override void Down()
        {
            DropColumn("dbo.Positions", "HasSecondaryStage");
        }
    }
}
