namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddQuestionnaireTemplates : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.QuestionnaireTemplates",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    CompanyId = c.Int(),
                    Name = c.String(nullable: false, maxLength: 150),
                    Description = c.String(maxLength: 500),
                    StageCount = c.Int(nullable: false, defaultValue: 1),
                    IsActive = c.Boolean(nullable: false, defaultValue: true),
                    CreatedOn = c.DateTime(nullable: false),
                    UpdatedOn = c.DateTime(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Companies", t => t.CompanyId)
                .Index(t => t.CompanyId);

            CreateTable(
                "dbo.QuestionnaireTemplateQuestions",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    TemplateId = c.Int(nullable: false),
                    QuestionId = c.Int(nullable: false),
                    Order = c.Int(nullable: false),
                    Weight = c.Decimal(precision: 18, scale: 2),
                    IsRequired = c.Boolean(nullable: false, defaultValue: true),
                    StageNumber = c.Int(nullable: false, defaultValue: 1),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.QuestionnaireTemplates", t => t.TemplateId, cascadeDelete: true)
                .ForeignKey("dbo.Questions", t => t.QuestionId)
                .Index(t => t.TemplateId)
                .Index(t => t.QuestionId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.QuestionnaireTemplateQuestions", "QuestionId", "dbo.Questions");
            DropForeignKey("dbo.QuestionnaireTemplateQuestions", "TemplateId", "dbo.QuestionnaireTemplates");
            DropForeignKey("dbo.QuestionnaireTemplates", "CompanyId", "dbo.Companies");
            DropIndex("dbo.QuestionnaireTemplateQuestions", new[] { "QuestionId" });
            DropIndex("dbo.QuestionnaireTemplateQuestions", new[] { "TemplateId" });
            DropIndex("dbo.QuestionnaireTemplates", new[] { "CompanyId" });
            DropTable("dbo.QuestionnaireTemplateQuestions");
            DropTable("dbo.QuestionnaireTemplates");
        }
    }
}
