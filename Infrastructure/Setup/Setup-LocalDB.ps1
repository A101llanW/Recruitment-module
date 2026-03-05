# Auto-configure LocalDB connection for HR application
Write-Host "Starting LocalDB configuration..."

# Start LocalDB instance
Write-Host "Starting LocalDB instance..."
sqllocaldb start MSSQLLocalDB

# Get pipe name
$pipeName = sqllocaldb info MSSQLLocalDB | Select-String "Instance pipe name"
Write-Host "Pipe name: $pipeName"

# Extract pipe name
if ($pipeName -match "Instance pipe name:\s*(.*)") {
    $actualPipe = $matches[1].Trim()
    Write-Host "Actual pipe: $actualPipe"
    
    # Update Web.config
    $webConfigPath = "c:\Users\allan\Documents\Examples\HR\HR.Web\Web.config"
    $configContent = Get-Content $webConfigPath
    
    # Update connection string
    $newConnectionString = "Data Source=$actualPipe;Initial Catalog=HR_Local;Integrated Security=True;MultipleActiveResultSets=True;Connect Timeout=60;"
    $configContent = $configContent -replace 'Data Source=.*?Initial Catalog=HR_Local', "Data Source=$actualPipe;Initial Catalog=HR_Local"
    
    Set-Content $webConfigPath $configContent
    Write-Host "Updated Web.config with pipe: $actualPipe"
    
    # Test connection
    Write-Host "Testing database connection..."
    sqlcmd -S "$actualPipe" -Q "SELECT 'Connection successful!'"
    
    Write-Host "Configuration complete!"
} else {
    Write-Host "Could not extract pipe name"
}
