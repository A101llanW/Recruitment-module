using System;
using System.Linq;
using HR.Web.Data;
using HR.Web.Models;

namespace HR.Web.Utilities
{
    public class ApplicationCleanup
    {
        public static void DeleteAllApplications()
        {
            using (var uow = new UnitOfWork())
            {
                try
                {
                    // Get all applications
                    var applications = uow.Applications.GetAll(a => a.Applicant, a => a.Position, a => a.Company).ToList();
                    
                    Console.WriteLine($"Found {applications.Count} applications:");
                    
                    foreach (var app in applications)
                    {
                        Console.WriteLine($"ID: {app.Id}, Applicant: {app.Applicant?.FullName}, Position: {app.Position?.Title}, Applied: {app.AppliedOn}");
                    }
                    
                    // Delete all applications
                    uow.Applications.RemoveRange(applications);
                    uow.Complete();
                    
                    Console.WriteLine($"Successfully deleted {applications.Count} applications.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
        
        public static void ListApplications()
        {
            using (var uow = new UnitOfWork())
            {
                try
                {
                    var applications = uow.Applications.GetAll(a => a.Applicant, a => a.Position, a => a.Company).ToList();
                    
                    Console.WriteLine($"=== Current Applications ({applications.Count}) ===");
                    foreach (var app in applications)
                    {
                        Console.WriteLine($"ID: {app.Id}");
                        Console.WriteLine($"  Applicant: {app.Applicant?.FullName ?? "Unknown"} ({app.Applicant?.Email})");
                        Console.WriteLine($"  Position: {app.Position?.Title}");
                        Console.WriteLine($"  Company: {app.Company?.Name}");
                        Console.WriteLine($"  Applied: {app.AppliedOn:yyyy-MM-dd HH:mm}");
                        Console.WriteLine($"  Status: {app.Status}");
                        Console.WriteLine($"  Score: {app.Score}");
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error listing applications: {ex.Message}");
                }
            }
        }
    }
}
