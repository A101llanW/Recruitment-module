namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddWeightToPositionQuestion : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PositionQuestions", "Weight", c => c.Decimal(precision: 18, scale: 2));
        }

        public override void Down()
        {
            DropColumn("dbo.PositionQuestions", "Weight");
        }
    }
}
