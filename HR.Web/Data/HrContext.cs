using System;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HR.Web.Models;
using Oracle.ManagedDataAccess.EntityFramework;

namespace HR.Web.Data
{
    //[DbConfigurationType(typeof(OracleEFConfiguration))] // Commented for local SQL testing; re-enable for Oracle
    public class HrContext : DbContext
    {
        public HrContext() : base(ResolveConnectionString())
        {
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Applicant> Applicants { get; set; }
        public DbSet<ApplicantProfile> ApplicantProfiles { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<Interview> Interviews { get; set; }
        public DbSet<Onboarding> Onboardings { get; set; }

        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<PositionQuestion> PositionQuestions { get; set; }
        public DbSet<RoleDefinition> RoleDefinitions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<ApplicationAnswer> ApplicationAnswers { get; set; }
        public DbSet<LoginAttempt> LoginAttempts { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<PasswordReset> PasswordResets { get; set; }
        public DbSet<LicenseTransaction> LicenseTransactions { get; set; }
        public DbSet<ImpersonationRequest> ImpersonationRequests { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<TemporaryCredential> TemporaryCredentials { get; set; }
        public DbSet<CompanyHrCcEmail> CompanyHrCcEmails { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //modelBuilder.HasDefaultSchema("HR_APP"); // Commented for local SQL testing; use default schema (dbo)

            modelBuilder.Entity<Application>()
                .HasRequired(a => a.Applicant)
                .WithMany(ap => ap.Applications)
                .HasForeignKey(a => a.ApplicantId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ApplicantProfile>()
                .HasRequired(p => p.Applicant)
                .WithMany()
                .HasForeignKey(p => p.ApplicantId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Application>()
                .HasRequired(a => a.Position)
                .WithMany(p => p.Applications)
                .HasForeignKey(a => a.PositionId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Interview>()
                .HasRequired(i => i.Application)
                .WithMany()
                .HasForeignKey(i => i.ApplicationId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Interview>()
                .HasRequired(i => i.Interviewer)
                .WithMany(u => u.Interviews)
                .HasForeignKey(i => i.InterviewerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Onboarding>()
                .HasRequired(o => o.Application)
                .WithMany()
                .HasForeignKey(o => o.ApplicationId)
                .WillCascadeOnDelete(false);

            // PositionQuestion relationships (disable cascade to prevent multiple cascade paths)
            modelBuilder.Entity<PositionQuestion>()
                .HasRequired(pq => pq.Position)
                .WithMany(p => p.PositionQuestions)
                .HasForeignKey(pq => pq.PositionId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PositionQuestion>()
                .HasRequired(pq => pq.Question)
                .WithMany(q => q.PositionQuestions)
                .HasForeignKey(pq => pq.QuestionId)
                .WillCascadeOnDelete(false);

            // Prevent multiple cascade path issues around questionnaire options
            modelBuilder.Entity<QuestionOption>()
                .HasRequired(qo => qo.Question)
                .WithMany(q => q.QuestionOptions) // Fix: Use the navigation property
                .HasForeignKey(qo => qo.QuestionId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<QuestionOption>()
                .HasMany(qo => qo.PositionQuestionOptions)
                .WithRequired(pqo => pqo.QuestionOption)
                .HasForeignKey(pqo => pqo.QuestionOptionId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PositionQuestionOption>()
                .HasRequired(pqo => pqo.PositionQuestion)
                .WithMany()
                .HasForeignKey(pqo => pqo.PositionQuestionId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PositionQuestionOption>()
                .HasRequired(pqo => pqo.QuestionOption)
                .WithMany(qo => qo.PositionQuestionOptions)
                .HasForeignKey(pqo => pqo.QuestionOptionId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RoleDefinition>()
                .HasOptional(r => r.Company)
                .WithMany(c => c.RoleDefinitions)
                .HasForeignKey(r => r.CompanyId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RoleDefinition>()
                .HasMany(r => r.RolePermissions)
                .WithRequired(p => p.RoleDefinition)
                .HasForeignKey(p => p.RoleDefinitionId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<User>()
                .HasOptional(u => u.RoleDefinition)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleDefinitionId)
                .WillCascadeOnDelete(false);

            // Prevent multiple cascade paths for Position -> Department
            modelBuilder.Entity<Position>()
                .HasRequired(p => p.Department)
                .WithMany(d => d.Positions)
                .HasForeignKey(p => p.DepartmentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<CompanyHrCcEmail>()
                .HasRequired(e => e.Company)
                .WithMany(c => c.HrCcEmails)
                .HasForeignKey(e => e.CompanyId)
                .WillCascadeOnDelete(true);
        }

        private static string ResolveConnectionString()
        {
            var configuredConnectionString = ConfigurationManager.ConnectionStrings["HrContext"]?.ConnectionString;
            if (!string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                return configuredConnectionString;
            }

            // EF6 command-line tools do not load a web project's Web.config automatically.
            var designTimeConnectionString = ResolveConnectionStringFromWebConfig();
            if (!string.IsNullOrWhiteSpace(designTimeConnectionString))
            {
                return designTimeConnectionString;
            }

            throw new ConfigurationErrorsException("No connection string named 'HrContext' could be found.");
        }

        private static string ResolveConnectionStringFromWebConfig()
        {
            foreach (var searchRoot in GetConnectionSearchRoots())
            {
                var current = new DirectoryInfo(searchRoot);
                for (var depth = 0; current != null && depth < 5; depth++, current = current.Parent)
                {
                    var connectionString = ReadConnectionStringFromConfig(Path.Combine(current.FullName, "Web.config"));
                    if (!string.IsNullOrWhiteSpace(connectionString))
                    {
                        return connectionString;
                    }
                }
            }

            return null;
        }

        private static string[] GetConnectionSearchRoots()
        {
            return new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Environment.CurrentDirectory,
                Path.GetDirectoryName(typeof(HrContext).Assembly.Location)
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        }

        private static string ReadConnectionStringFromConfig(string configPath)
        {
            if (!File.Exists(configPath))
            {
                return null;
            }

            try
            {
                var config = XDocument.Load(configPath);
                return config.Root?
                    .Element("connectionStrings")?
                    .Elements("add")
                    .Where(element => string.Equals((string)element.Attribute("name"), "HrContext", StringComparison.Ordinal))
                    .Select(element => (string)element.Attribute("connectionString"))
                    .FirstOrDefault(connectionString => !string.IsNullOrWhiteSpace(connectionString));
            }
            catch
            {
                return null;
            }
        }
    }
}
