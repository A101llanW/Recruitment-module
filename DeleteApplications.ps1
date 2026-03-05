# HR Application Cleanup Script
# This script will list and optionally delete all applications in the HR database

param(
    [switch]$Delete,
    [switch]$ListOnly
)

# Database connection string
$connectionString = "Data Source=.;Initial Catalog=HR_Local;Integrated Security=True;MultipleActiveResultSets=True;Connect Timeout=30;"

Write-Host "HR Application Cleanup Utility" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green
Write-Host ""

try {
    # Load required assemblies
    Add-Type -AssemblyName System.Data
    Add-Type -AssemblyName System.Core
    
    # Create connection
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    if ($ListOnly -or -not $Delete) {
        Write-Host "Current applications in database:" -ForegroundColor Yellow
        
        $query = @"
        SELECT 
            a.Id,
            ap.FullName as ApplicantName,
            ap.Email as ApplicantEmail,
            p.Title as PositionTitle,
            c.Name as CompanyName,
            a.AppliedOn,
            a.Status,
            a.Score
        FROM Applications a
        LEFT JOIN Applicants ap ON a.ApplicantId = ap.Id
        LEFT JOIN Positions p ON a.PositionId = p.Id
        LEFT JOIN Companies c ON p.CompanyId = c.Id
        ORDER BY a.AppliedOn DESC
"@
        
        $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
        $reader = $command.ExecuteReader()
        
        $applications = @()
        while ($reader.Read()) {
            $app = @{
                Id = $reader["Id"]
                ApplicantName = if ($reader["ApplicantName"] -ne [DBNull]::Value) { $reader["ApplicantName"] } else { "Unknown" }
                ApplicantEmail = if ($reader["ApplicantEmail"] -ne [DBNull]::Value) { $reader["ApplicantEmail"] } else { "N/A" }
                PositionTitle = if ($reader["PositionTitle"] -ne [DBNull]::Value) { $reader["PositionTitle"] } else { "Unknown" }
                CompanyName = if ($reader["CompanyName"] -ne [DBNull]::Value) { $reader["CompanyName"] } else { "Unknown" }
                AppliedOn = if ($reader["AppliedOn"] -ne [DBNull]::Value) { [DateTime]$reader["AppliedOn"] } else { [DateTime]::MinValue }
                Status = if ($reader["Status"] -ne [DBNull]::Value) { $reader["Status"] } else { "Unknown" }
                Score = if ($reader["Score"] -ne [DBNull]::Value) { $reader["Score"] } else { $null }
            }
            $applications += $app
        }
        $reader.Close()
        
        if ($applications.Count -eq 0) {
            Write-Host "No applications found in the database." -ForegroundColor Green
        } else {
            Write-Host "Found $($applications.Count) applications:" -ForegroundColor Yellow
            Write-Host ""
            
            foreach ($app in $applications) {
                Write-Host "ID: $($app.Id)" -ForegroundColor Cyan
                Write-Host "  Applicant: $($app.ApplicantName) ($($app.ApplicantEmail))"
                Write-Host "  Position: $($app.PositionTitle)"
                Write-Host "  Company: $($app.CompanyName)"
                Write-Host "  Applied: $($app.AppliedOn.ToString('yyyy-MM-dd HH:mm'))"
                Write-Host "  Status: $($app.Status)"
                if ($app.Score -ne $null) {
                    Write-Host "  Score: $($app.Score)"
                }
                Write-Host ""
            }
        }
    }
    
    if ($Delete -and $applications.Count -gt 0) {
        Write-Host "WARNING: This will delete ALL $($applications.Count) applications!" -ForegroundColor Red
        Write-Host "This action cannot be undone." -ForegroundColor Red
        Write-Host ""
        Write-Host "Type 'DELETE-ALL-APPLICATIONS' to confirm: " -ForegroundColor Red -NoNewline
        $confirmation = Read-Host
        
        if ($confirmation -eq "DELETE-ALL-APPLICATIONS") {
            Write-Host "Deleting applications..." -ForegroundColor Yellow
            
            $deleteQuery = "DELETE FROM Applications"
            $deleteCommand = New-Object System.Data.SqlClient.SqlCommand($deleteQuery, $connection)
            $deletedRows = $deleteCommand.ExecuteNonQuery()
            
            Write-Host "Successfully deleted $deletedRows applications." -ForegroundColor Green
            
            # Show remaining applications
            Write-Host ""
            Write-Host "Applications after deletion:" -ForegroundColor Yellow
            $checkCommand = New-Object System.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM Applications", $connection)
            $remainingCount = $checkCommand.ExecuteScalar()
            Write-Host "Remaining applications: $remainingCount" -ForegroundColor Green
        } else {
            Write-Host "Deletion cancelled. Confirmation text did not match." -ForegroundColor Yellow
        }
    }
    
    $connection.Close()
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}

Write-Host ""
Write-Host "Script completed." -ForegroundColor Green
