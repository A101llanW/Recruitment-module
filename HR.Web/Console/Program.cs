using System;
using HR.Web.Utilities;

namespace HR.Web.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("HR Application Cleanup Utility");
            Console.WriteLine("==============================");
            Console.WriteLine();
            
            Console.WriteLine("Current applications in database:");
            ApplicationCleanup.ListApplications();
            
            Console.WriteLine();
            Console.Write("Do you want to delete all applications? (y/N): ");
            var response = Console.ReadLine();
            
            if (response?.ToLower() == "y" || response?.ToLower() == "yes")
            {
                Console.WriteLine("Deleting all applications...");
                ApplicationCleanup.DeleteAllApplications();
                
                Console.WriteLine();
                Console.WriteLine("Applications after deletion:");
                ApplicationCleanup.ListApplications();
            }
            else
            {
                Console.WriteLine("No applications deleted.");
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
