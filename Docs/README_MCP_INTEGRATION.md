# Recruitment Module - MCP Integration Documentation

## 📋 Overview

The Recruitment Module is a comprehensive ASP.NET MVC application for managing job applications, candidates, and positions. This document outlines the current system architecture and features.

## 🏗️ Project Structure

```
HR/
├── HR.Web/                          # Main Web Application
│   ├── Controllers/
│   │   ├── AccountController.cs           # Authentication & password management
│   │   ├── AdminController.cs             # Admin functionality
│   │   ├── AdminController.MCP.cs         # MCP-enhanced admin features
│   │   ├── AdminController.Scoring.cs     # Advanced candidate scoring
│   │   ├── ApplicationsController.cs      # Job application management
│   │   ├── PositionsController.cs         # Position management
│   │   └── QuestionnaireController.cs      # Questionnaire system
│   ├── Helpers/
│   │   └── PasswordHelper.cs              # Secure password hashing & validation
│   ├── Models/
│   │   ├── ChangePasswordViewModel.cs      # Password change model
│   │   ├── RegisterViewModel.cs           # User registration model
│   │   └── User.cs                        # User entity model
│   ├── Services/
│   │   ├── ReportService.cs               # Report generation (CSV/PDF)
│   │   ├── DynamicQuestionService.cs      # Dynamic question generation
│   │   └── [Other Services]               # Various business logic services
│   ├── Views/
│   │   ├── Account/
│   │   │   ├── Login.cshtml               # Login page with password toggle
│   │   │   ├── ChangePassword.cshtml       # Password change interface
│   │   │   ├── Register.cshtml             # User registration
│   │   │   └── Index.cshtml               # Account overview
│   │   ├── Admin/
│   │   │   └── [Admin Views]              # Administrative interfaces
│   │   ├── Applications/
│   │   │   └── [Application Views]        # Application management
│   │   ├── Positions/
│   │   │   └── [Position Views]           # Position management
│   │   └── Shared/
│   │       └── _Layout.cshtml              # Master layout
│   ├── Migrations/
│   │   └── 202502020000000_AddPasswordChangeFields.cs  # DB migration
│   ├── App_Data/
│   │   └── Resumes/                       # Uploaded resume files
│   ├── Reports/                           # Generated reports
│   ├── Web.config                         # Application configuration
│   └── Global.asax.cs                     # Application startup
├── HR.sln                                # Solution file
├── packages/                             # NuGet packages
├── [SQL Scripts]                         # Database setup scripts
└── [PowerShell Scripts]                  # Database management scripts
```

## 🔐 Security Features

### Password Management System
- **Enhanced Password Hashing**: PBKDF2 with 100,000 iterations
- **Default Password System**: All users can login with "Temp123!" 
- **Forced Password Changes**: Users must change password on first login
- **Password Strength Validation**: 8+ characters with multiple character types
- **Real-time Strength Indicators**: Color-coded password feedback
- **Password Visibility Toggles**: Eye icons on all password fields

### Authentication & Authorization
- **Role-based Access**: Admin and Client roles
- **Secure Authentication Cookies**: 8-hour sessions
- **Anti-forgery Token Protection**: CSRF prevention
- **Account Lockout Protection**: Brute force prevention
- **Comprehensive Audit Logging**: All security events tracked

## 🚀 Key Features

### User Management
- **Registration System**: New user account creation
- **Login/Logout**: Secure authentication
- **Password Reset**: Secure password recovery
- **Profile Management**: User information updates

### Position Management
- **Job Posting**: Create and manage job positions
- **Department Organization**: Categorize positions by department
- **Position Details**: Comprehensive job descriptions
- **Application Tracking**: Monitor applications per position

### Application Management
- **Application Submission**: Candidates apply for positions
- **Resume Upload**: File attachment support
- **Candidate Evaluation**: Scoring and assessment

### Questionnaire System
- **Dynamic Questions**: AI-powered question generation
- **Custom Questionnaires**: Position-specific assessments
- **Candidate Testing**: Interactive testing interface
- **Result Analysis**: Detailed test results and scoring

### Reporting System
- **Candidate Reports**: Comprehensive candidate data
- **Application Reports**: Application status and metrics
- **Interview Reports**: Interview scheduling and results
- **Department Reports**: Department-wise analytics
- **PDF/CSV Export**: Multiple format support

## 🛠 Database Setup

### Prerequisites
- SQL Server Express or SQL Server
- Visual Studio 2019+ or Visual Studio Code

### Setup Scripts
- `add_password_columns.sql` - Add password security columns
- `grant_permissions.sql` - Set database permissions
- `Setup-LocalDB.ps1` - Automated database setup

### Migration
- Entity Framework Code First migrations
- Automatic schema updates
- Data seeding capabilities

## 🔧 Configuration

### Web.config Settings
- Database connection strings
- Authentication configuration
- File upload settings
- Security parameters

### Environment Setup
- IIS Express development server
- Local SQL Express database
- Debug configuration enabled

## 📊 Default Users

The system comes with pre-configured users for testing:

### Admin Users
- **admin** / **Temp123!** - System administrator
- **hr** / **Temp123!** - HR administrator

### Client Users
- **client** / **Temp123!** - Client user
- **wambua** / **Temp123!** - Client user
- **Monday** / **Temp123!** - Client user
- **Tuesday** / **Temp123!** - Client user
- **Wednesday** / **Temp123!** - Client user
- **TClient** / **Temp123!** - Client user

*Note: All users must change their password on first login.*

## 🎯 Getting Started

### 1. Clone Repository
```bash
git clone https://github.com/A101llan/Recruitment-module.git
cd Recruitment-module
```

### 2. Setup Database
```powershell
# Run database setup
.\Setup-LocalDB.ps1

# Or manually execute SQL scripts
sqlcmd -S ".\SQLEXPRESS" -i add_password_columns.sql
sqlcmd -S ".\SQLEXPRESS" -i grant_permissions.sql
```

### 3. Open Solution
- Open `HR.sln` in Visual Studio
- Restore NuGet packages
- Build the solution

### 4. Run Application
- Press F5 in Visual Studio
- Or use `dotnet run` in the HR.Web directory
- Application runs on `http://localhost:8080`

## 🔍 Development Notes

### Password Security Implementation
- Uses PBKDF2 with SHA256
- 100,000 iterations for enhanced security
- 256-bit key generation
- Per-user random salt generation

### Frontend Technologies
- Bootstrap 4 for responsive design
- Font Awesome for icons
- jQuery for JavaScript interactions
- Razor view engine for server-side rendering

### Backend Technologies
- ASP.NET MVC 5
- Entity Framework 6
- SQL Server for data storage
- PowerShell for automation scripts

## 📝 Recent Updates

### Security Enhancements
- ✅ Implemented comprehensive password security system
- ✅ Added default password functionality
- ✅ Enhanced password validation (8+ characters)
- ✅ Added password visibility toggles
- ✅ Implemented forced password changes

### UI/UX Improvements
- ✅ Enhanced login interface with password toggle
- ✅ Improved password change workflow
- ✅ Added real-time password strength indicators


## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 📄 License

This project is proprietary software for Nanosoft Technologies recruitment management.

---

**Last Updated**: February 2026
**Version**: 2.0
**Framework**: ASP.NET MVC 5
**Database**: SQL Server Express
