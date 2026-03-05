param(
    [switch]$Confirm
)

$connectionString = "Data Source=.;Initial Catalog=HR_Local;Integrated Security=True;MultipleActiveResultSets=True;Connect Timeout=30;"

Write-Host "HR Application Deletion Script" -ForegroundColor Green
Write-Host "=============================" -ForegroundColor Green

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    # Count applications first
    $countCommand = New-Object System.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM Applications", $connection)
    $appCount = $countCommand.ExecuteScalar()
    
    Write-Host "Found $appCount applications in the database." -ForegroundColor Yellow
    
    if ($appCount -gt 0) {
        if ($Confirm) {
            Write-Host "Deleting all applications..." -ForegroundColor Red
            
            # Delete applications
            $deleteCommand = New-Object System.Data.SqlClient.SqlCommand("DELETE FROM Applications", $connection)
            $deletedRows = $deleteCommand.ExecuteNonQuery()
            
            Write-Host "Successfully deleted $deletedRows applications." -ForegroundColor Green
            
            # Verify deletion
            $verifyCommand = New-Object System.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM Applications", $connection)
            $remainingCount = $verifyCommand.ExecuteScalar()
            Write-Host "Remaining applications: $remainingCount" -ForegroundColor Green
        } else {
            Write-Host "Use -Confirm switch to actually delete the applications." -ForegroundColor Yellow
        }
    } else {
        Write-Host "No applications to delete." -ForegroundColor Green
    }
    
    $connection.Close()
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}
