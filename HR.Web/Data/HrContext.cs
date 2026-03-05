using System.Data.Entity;
using HR.Web.Models;
using Oracle.ManagedDataAccess.EntityFramework;

namespace HR.Web.Data
{
    //[DbConfigurationType(typeof(OracleEFConfiguration))] // Commented for local SQL testing; re-enable for Oracle
    public class HrContext : DbContext
    {
        public HrContext() : base("name=HrContext")
        {
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Applicant> Applicants { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<Interview> Interviews { get; set; }
        public DbSet<Onboarding> Onboardings { get; set; }

        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<PositionQuestion> PositionQuestions { get; set; }
        public DbSet<ApplicationAnswer> ApplicationAnswers { get; set; }
        public DbSet<LoginAttempt> LoginAttempts { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<PasswordReset> PasswordResets { get; set; }
        public DbSet<LicenseTransaction> LicenseTransactions { get; set; }
        public DbSet<ImpersonationRequest> ImpersonationRequests { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //modelBuilder.HasDefaultSchema("HR_APP"); // Commented for local SQL testing; use default schema (dbo)

            modelBuilder.Entity<Application>()
                .HasRequired(a => a.Applicant)
                .WithMany(ap => ap.Applications)
                .HasForeignKey(a => a.ApplicantId)
                .WillCascadeOnDelete(false);

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

            // Prevent multiple cascade paths for Position -> Department
            modelBuilder.Entity<Position>()
                .HasRequired(p => p.Department)
                .WithMany(d => d.Positions)
                .HasForeignKey(p => p.DepartmentId)
                .WillCascadeOnDelete(false);
        }
    }
}


