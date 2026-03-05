using System;
using HR.Web.Models;

namespace HR.Web.Data
{
    public class UnitOfWork : IDisposable
    {
        private readonly HrContext _context = new HrContext();

        public Repository<Company> Companies { get { return new Repository<Company>(_context); } }
        public Repository<User> Users { get { return new Repository<User>(_context); } }
        public Repository<Department> Departments { get { return new Repository<Department>(_context); } }
        public Repository<Position> Positions { get { return new Repository<Position>(_context); } }
        public Repository<Applicant> Applicants { get { return new Repository<Applicant>(_context); } }
        public Repository<Application> Applications { get { return new Repository<Application>(_context); } }
        public Repository<Interview> Interviews { get { return new Repository<Interview>(_context); } }
        public Repository<Onboarding> Onboardings { get { return new Repository<Onboarding>(_context); } }

        public Repository<Question> Questions { get { return new Repository<Question>(_context); } }
        public Repository<PositionQuestion> PositionQuestions { get { return new Repository<PositionQuestion>(_context); } }
        public Repository<PositionQuestionOption> PositionQuestionOptions { get { return new Repository<PositionQuestionOption>(_context); } }
        public Repository<ApplicationAnswer> ApplicationAnswers { get { return new Repository<ApplicationAnswer>(_context); } }
        public Repository<LoginAttempt> LoginAttempts { get { return new Repository<LoginAttempt>(_context); } }
        public Repository<AuditLog> AuditLogs { get { return new Repository<AuditLog>(_context); } }
        public Repository<Report> Reports { get { return new Repository<Report>(_context); } }
        public Repository<PasswordReset> PasswordResets { get { return new Repository<PasswordReset>(_context); } }
        public Repository<LicenseTransaction> LicenseTransactions { get { return new Repository<LicenseTransaction>(_context); } }
        public Repository<ImpersonationRequest> ImpersonationRequests { get { return new Repository<ImpersonationRequest>(_context); } }

        // Expose the underlying context for advanced queries (e.g., Question options)
        public HrContext Context { get { return _context; } }

        public int Complete()
        {
            return _context.SaveChanges();
        }

        /// <summary>
        /// Execute raw SQL command
        /// </summary>
        public void ExecuteSql(string sql)
        {
            _context.Database.ExecuteSqlCommand(sql);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}