namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddDateOfBirthToUsersAndApplicants : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "DateOfBirth", c => c.DateTime());
            AddColumn("dbo.Applicants", "DateOfBirth", c => c.DateTime());
        }

        public override void Down()
        {
            DropColumn("dbo.Applicants", "DateOfBirth");
            DropColumn("dbo.Users", "DateOfBirth");
        }
    }
}
