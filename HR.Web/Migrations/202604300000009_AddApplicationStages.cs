namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddApplicationStages : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PositionQuestions", "StageNumber", c => c.Int(nullable: false, defaultValue: 1));

            AddColumn("dbo.Applications", "CurrentStage", c => c.Int(nullable: false, defaultValue: 1));
            AddColumn("dbo.Applications", "IsStageTwoInvited", c => c.Boolean(nullable: false, defaultValue: false));
            AddColumn("dbo.Applications", "StageTwoInvitedOn", c => c.DateTime());
            AddColumn("dbo.Applications", "StageTwoCompletedOn", c => c.DateTime());
            AddColumn("dbo.Applications", "StageTwoScore", c => c.Decimal(precision: 18, scale: 2));

            AddColumn("dbo.ApplicationAnswers", "StageNumber", c => c.Int(nullable: false, defaultValue: 1));
        }

        public override void Down()
        {
            DropColumn("dbo.ApplicationAnswers", "StageNumber");

            DropColumn("dbo.Applications", "StageTwoScore");
            DropColumn("dbo.Applications", "StageTwoCompletedOn");
            DropColumn("dbo.Applications", "StageTwoInvitedOn");
            DropColumn("dbo.Applications", "IsStageTwoInvited");
            DropColumn("dbo.Applications", "CurrentStage");

            DropColumn("dbo.PositionQuestions", "StageNumber");
        }
    }
}
