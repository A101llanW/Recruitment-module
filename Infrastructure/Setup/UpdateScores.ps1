# Direct Score Update Script
# This script uses the enhanced scoring algorithm to recalculate all application scores

using namespace HR.Web.Data
using namespace HR.Web.Services
using namespace HR.Web.Models

Add-Type -Path "c:\Users\allan\Documents\Examples\HR\HR.Web\bin\HR.Web.dll"
Add-Type -Path "c:\Users\allan\Documents\Examples\HR\packages\EntityFramework.6.4.4\lib\net45\EntityFramework.dll"
Add-Type -Path "c:\Users\allan\Documents\Examples\HR\packages\EntityFramework.6.4.4\lib\net45\EntityFramework.SqlServer.dll"

Write-Host "=== HR Scoring System - Enhanced Algorithm Score Update ===" -ForegroundColor Green

try {
    # Initialize services
    $uow = New-Object UnitOfWork
    $scoringService = New-Object ScoringService
    
    Write-Host "Retrieving applications for Full-Stack Developer position (ID: 6)..." -ForegroundColor Yellow
    
    # Get applications for Full-Stack Developer position
    $applications = $uow.Applications.GetAll().Where({ param($app) $app.PositionId -eq 6 }).ToList()
    
    Write-Host "Found $($applications.Count) applications" -ForegroundColor Cyan
    
    $updatedCount = 0
    $results = @()
    
    foreach ($application in $applications) {
        $applicantName = if ($application.Applicant) { $application.Applicant.FullName } else { "Unknown" }
        Write-Host "Processing application $($application.Id) - $applicantName" -ForegroundColor White
        
        # Calculate new score using enhanced algorithm
        $newScore = $scoringService.CalculateApplicationScore($application)
        $oldScore = $application.Score
        
        $result = @{
            ApplicationId = $application.Id
            ApplicantName = $applicantName
            OldScore = $oldScore
            NewScore = $newScore
            Difference = $newScore - $oldScore
        }
        
        $results += $result
        
        if ($application.Score -ne $newScore) {
            $application.Score = $newScore
            $uow.Applications.Update($application)
            $updatedCount++
            
            Write-Host "  Updated: $($oldScore) → $($newScore) (Difference: $($newScore - $oldScore))" -ForegroundColor Green
        } else {
            Write-Host "  No change: $($oldScore)" -ForegroundColor Gray
        }
    }
    
    # Save changes
    $uow.Complete()
    
    Write-Host "`n=== SCORE UPDATE SUMMARY ===" -ForegroundColor Green
    Write-Host "Total applications processed: $($applications.Count)" -ForegroundColor Cyan
    Write-Host "Applications updated: $updatedCount" -ForegroundColor Green
    Write-Host "Applications unchanged: $($applications.Count - $updatedCount)" -ForegroundColor Gray
    
    Write-Host "`n=== DETAILED RESULTS ===" -ForegroundColor Yellow
    foreach ($result in $results) {
        $color = if ($result.Difference -gt 0) { "Green" } 
                 elseif ($result.Difference -lt 0) { "Red" } 
                 else { "Gray" }
        
        Write-Host "Application $($result.ApplicationId): $($result.ApplicantName)" -ForegroundColor White
        Write-Host "  Score: $($result.OldScore) → $($result.NewScore)" -ForegroundColor $color
        if ($result.Difference -ne 0) {
            Write-Host "  Change: $($result.Difference)" -ForegroundColor $color
        }
    }
    
    Write-Host "`nScore update completed successfully!" -ForegroundColor Green
    
}
catch {
    Write-Host "Error during score update: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.Exception.StackTrace)" -ForegroundColor Red
}
