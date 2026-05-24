namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddLegalAcceptanceTracking : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "PrivacyAcceptedAt", c => c.DateTime());
            AddColumn("dbo.Users", "TermsAcceptedAt", c => c.DateTime());
            AddColumn("dbo.Users", "PrivacyVersion", c => c.String(maxLength: 20));
            AddColumn("dbo.Users", "TermsVersion", c => c.String(maxLength: 20));

            AddColumn("dbo.Applicants", "PrivacyAcceptedAt", c => c.DateTime());
            AddColumn("dbo.Applicants", "TermsAcceptedAt", c => c.DateTime());
            AddColumn("dbo.Applicants", "PrivacyVersion", c => c.String(maxLength: 20));
            AddColumn("dbo.Applicants", "TermsVersion", c => c.String(maxLength: 20));
        }

        public override void Down()
        {
            DropColumn("dbo.Applicants", "TermsVersion");
            DropColumn("dbo.Applicants", "PrivacyVersion");
            DropColumn("dbo.Applicants", "TermsAcceptedAt");
            DropColumn("dbo.Applicants", "PrivacyAcceptedAt");

            DropColumn("dbo.Users", "TermsVersion");
            DropColumn("dbo.Users", "PrivacyVersion");
            DropColumn("dbo.Users", "TermsAcceptedAt");
            DropColumn("dbo.Users", "PrivacyAcceptedAt");
        }
    }
}
