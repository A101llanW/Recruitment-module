namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddCoverLetterToApplications : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Applications", "CoverLetter", c => c.String());
        }

        public override void Down()
        {
            DropColumn("dbo.Applications", "CoverLetter");
        }
    }
}

