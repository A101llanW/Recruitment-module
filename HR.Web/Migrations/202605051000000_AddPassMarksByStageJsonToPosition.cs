namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddPassMarksByStageJsonToPosition : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Positions", "PassMarksByStageJson", c => c.String(maxLength: 4000, nullable: true));
        }

        public override void Down()
        {
            DropColumn("dbo.Positions", "PassMarksByStageJson");
        }
    }
}
