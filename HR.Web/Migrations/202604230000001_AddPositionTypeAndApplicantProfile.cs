namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddPositionTypeAndApplicantProfile : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Positions", "IsTechnical", c => c.Boolean());

            CreateTable(
                "dbo.ApplicantProfiles",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ApplicantId = c.Int(nullable: false),
                        Location = c.String(maxLength: 200),
                        TotalYearsExperience = c.Decimal(precision: 5, scale: 2),
                        RelevantYearsExperience = c.Decimal(precision: 5, scale: 2),
                        MostRecentCompany = c.String(maxLength: 200),
                        MostRecentTitle = c.String(maxLength: 150),
                        MostRecentStartDate = c.DateTime(),
                        MostRecentEndDate = c.DateTime(),
                        SecondMostRecentCompany = c.String(maxLength: 200),
                        SecondMostRecentTitle = c.String(maxLength: 150),
                        SecondMostRecentStartDate = c.DateTime(),
                        SecondMostRecentEndDate = c.DateTime(),
                        EmploymentType = c.String(maxLength: 50),
                        Skills = c.String(maxLength: 1000),
                        Competencies = c.String(maxLength: 1000),
                        EducationDegree = c.String(maxLength: 200),
                        EducationInstitution = c.String(maxLength: 200),
                        KeyAchievement = c.String(maxLength: 500),
                        Certifications = c.String(maxLength: 500),
                        PortfolioUrl = c.String(maxLength: 300),
                        WorkAuthorization = c.Boolean(nullable: false),
                        NoticePeriod = c.String(maxLength: 100),
                        CreatedOn = c.DateTime(nullable: false),
                        UpdatedOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Applicants", t => t.ApplicantId, cascadeDelete: true)
                .Index(t => t.ApplicantId, unique: true, name: "IX_ApplicantProfile_Applicant");
        }

        public override void Down()
        {
            DropForeignKey("dbo.ApplicantProfiles", "ApplicantId", "dbo.Applicants");
            DropIndex("dbo.ApplicantProfiles", "IX_ApplicantProfile_Applicant");
            DropTable("dbo.ApplicantProfiles");
            DropColumn("dbo.Positions", "IsTechnical");
        }
    }
}
