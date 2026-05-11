namespace HR.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class QuestionnaireMultiStage : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Positions", "QuestionnaireStageCount", c => c.Int(nullable: false, defaultValue: 1));
            Sql("UPDATE dbo.Positions SET QuestionnaireStageCount = CASE WHEN HasSecondaryStage = 1 THEN 2 ELSE 1 END");
            DropColumn("dbo.Positions", "HasSecondaryStage");

            AddColumn("dbo.Applications", "PendingQuestionnaireStage", c => c.Int());
            AddColumn("dbo.Applications", "LastCompletedQuestionnaireStage", c => c.Int(nullable: false, defaultValue: 0));
            AddColumn("dbo.Applications", "QuestionnaireInvitedOn", c => c.DateTime());
            AddColumn("dbo.Applications", "LastQuestionnaireScore", c => c.Decimal(precision: 18, scale: 2));

            Sql(@"
UPDATE dbo.Applications SET
    LastCompletedQuestionnaireStage = CASE
        WHEN StageTwoCompletedOn IS NOT NULL THEN 2
        WHEN IsStageTwoInvited = 1 THEN 1
        ELSE 0 END,
    PendingQuestionnaireStage = CASE
        WHEN IsStageTwoInvited = 1 AND StageTwoCompletedOn IS NULL THEN 2
        ELSE NULL END,
    QuestionnaireInvitedOn = StageTwoInvitedOn,
    LastQuestionnaireScore = StageTwoScore
");

            DropColumn("dbo.Applications", "IsStageTwoInvited");
            DropColumn("dbo.Applications", "StageTwoInvitedOn");
            DropColumn("dbo.Applications", "StageTwoCompletedOn");
            DropColumn("dbo.Applications", "StageTwoScore");
        }

        public override void Down()
        {
            throw new NotSupportedException("Reverting QuestionnaireMultiStage is not supported.");
        }
    }
}
