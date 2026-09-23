namespace HR.Web.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddPositionViews : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "SuccessfulLoginCount", c => c.Int(nullable: false, defaultValue: 0));
            CreateTable(
                "dbo.PositionViews",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    UserId = c.Int(nullable: false),
                    PositionId = c.Int(nullable: false),
                    ViewedAtUtc = c.DateTime(nullable: false, precision: 0, storeType: "datetime2"),
                    LoginCountAtView = c.Int(nullable: false),
                    IsOpenAtView = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.UserId)
                .ForeignKey("dbo.Positions", t => t.PositionId)
                .Index(t => new { t.UserId, t.PositionId }, unique: true, name: "UX_PositionViews_User_Position");
        }

        public override void Down()
        {
            DropForeignKey("dbo.PositionViews", "PositionId", "dbo.Positions");
            DropForeignKey("dbo.PositionViews", "UserId", "dbo.Users");
            DropIndex("dbo.PositionViews", "UX_PositionViews_User_Position");
            DropTable("dbo.PositionViews");
            DropColumn("dbo.Users", "SuccessfulLoginCount");
        }
    }
}
