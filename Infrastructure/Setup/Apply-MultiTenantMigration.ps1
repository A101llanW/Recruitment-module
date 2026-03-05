# Apply Multi-Tenant Migration
Write-Host "Applying Multi-Tenant Migration..." -ForegroundColor Cyan

$sqlFile = "c:\Users\allan\Documents\Examples\HR\HR.Web\Migrations\MultiTenantMigration.sql"
$server = "."
$database = "HR_Local"

try {
    Write-Host "Connecting to $server..." -ForegroundColor Yellow
    sqlcmd -S $server -d $database -i $sqlFile -b
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Migration applied successfully!" -ForegroundColor Green
    }
    else {
        Write-Host "Migration failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    }
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
