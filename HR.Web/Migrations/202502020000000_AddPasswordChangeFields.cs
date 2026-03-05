namespace HR.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPasswordChangeFields : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "RequirePasswordChange", c => c.Boolean(nullable: false, defaultValue: false));
            AddColumn("dbo.Users", "LastPasswordChange", c => c.DateTime());
            AddColumn("dbo.Users", "PasswordChangeExpiry", c => c.DateTime());
            
            // Set RequirePasswordChange to true for existing users with weak passwords
            Sql(@"
                UPDATE Users 
                SET RequirePasswordChange = 1, 
                    PasswordChangeExpiry = DATEADD(day, 7, GETUTCDATE())
                WHERE RequirePasswordChange = 0 
                AND (
                    PasswordHash LIKE '%10000%' -- Old iteration count
                    OR LEN(PasswordHash) < 50 -- Likely weak passwords
                )
            ");
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "PasswordChangeExpiry");
            DropColumn("dbo.Users", "LastPasswordChange");
            DropColumn("dbo.Users", "RequirePasswordChange");
        }
    }
}
