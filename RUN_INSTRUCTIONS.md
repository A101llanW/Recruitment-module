# How to Run the HR Web Application

## 🎯 **Issue Identified**
This is a .NET Framework 4.7 MVC application, NOT a .NET Core application. It cannot be run with `dotnet run`.

## ✅ **Build Status**
- **Build**: ✅ Successful (0 errors, 0 warnings)
- **Configuration**: ✅ Release mode
- **Dependencies**: ✅ All resolved
- **Database**: ✅ HR_Local (SQL Server)

## 🚀 **How to Run**

### **Method 1: Visual Studio (Recommended)**
1. Open `HR.sln` in Visual Studio
2. Set `HR.Web` as startup project
3. Press `F5` or click "Start Debugging"
4. IIS Express will start automatically

### **Method 2: IIS Express Command Line**
```cmd
cd C:\Program Files\IIS Express
iisexpress.exe /site:"HR.Web" /config:"C:\path\to\HR.Web\.vs\config\applicationhost.config"
```

### **Method 3: Local IIS**
1. Open IIS Manager
2. Add Website pointing to `HR.Web` folder
3. Configure port (e.g., 8080)
4. Browse the site

## 🔧 **Troubleshooting**

### **IIS Express Exits Immediately**
1. **Check Database Connection**: Ensure SQL Server is running
2. **Verify Web.Config**: Connection string should point to valid SQL Server
3. **Check Permissions**: Ensure IIS Express has read permissions
4. **Port Conflicts**: Make sure port 5002 isn't in use

### **Database Setup**
```sql
-- Create database if it doesn't exist
CREATE DATABASE HR_Local;
GO

-- Verify connection
USE HR_Local;
SELECT 1;
```

### **Web.Config Check**
- `HrContext` connection string should be valid
- `debug="true"` for development
- `targetFramework="4.7"` matches installed .NET Framework

## 📁 **Project Structure**
```
HR.Web/
├── Controllers/     # MVC Controllers
├── Views/          # Razor Views
├── Models/         # View Models
├── Services/       # Business Logic
├── Data/           # Entity Framework
├── Utilities/      # Helper Classes
├── web.config      # Configuration
└── Global.asax.cs # Application Startup
```

## 🎯 **Next Steps**
1. Open in Visual Studio
2. Press F5 to run
3. Navigate to login page
4. Test email verification (check VS Output for OTP codes)

## ✅ **What's Working**
- Modern CAPTCHA system with case-sensitive validation
- Email verification with debug logging
- Department deletion with proper error handling
- Login page with clean, centered interface
- All compilation errors fixed
