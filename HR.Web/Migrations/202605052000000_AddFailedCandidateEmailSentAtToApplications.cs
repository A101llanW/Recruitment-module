namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddFailedCandidateEmailSentAtToApplications : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Applications", "FailedCandidateEmailSentAt", c => c.DateTime());
        }

        public override void Down()
        {
            DropColumn("dbo.Applications", "FailedCandidateEmailSentAt");
        }
    }
}
