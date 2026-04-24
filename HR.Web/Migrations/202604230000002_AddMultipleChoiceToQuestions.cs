namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddMultipleChoiceToQuestions : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Questions", "AllowMultipleChoices", c => c.Boolean(nullable: false, defaultValue: false));
        }

        public override void Down()
        {
            DropColumn("dbo.Questions", "AllowMultipleChoices");
        }
    }
}
