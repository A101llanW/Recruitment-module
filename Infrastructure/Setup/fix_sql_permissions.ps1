# Grant SQL Server permissions to IIS Express application pool
$serverInstance = ".\SQLEXPRESS"
$loginName = "IIS APPPOOL\.NET v4.5"
$databaseName = "HR_Local"

try {
    # Create the login
    $createLoginQuery = "CREATE LOGIN [$loginName] FROM WINDOWS;"
    Invoke-Sqlcmd -ServerInstance $serverInstance -Query $createLoginQuery -ErrorAction Stop
    Write-Host "Created login for $loginName"
    
    # Grant access to the database
    $useDatabaseQuery = "USE [$databaseName];"
    $createUserQuery = "CREATE USER [$loginName] FOR LOGIN [$loginName];"
    $grantRoleQuery = "ALTER ROLE db_owner ADD MEMBER [$loginName];"
    
    Invoke-Sqlcmd -ServerInstance $serverInstance -Query "$useDatabaseQuery $createUserQuery $grantRoleQuery" -ErrorAction Stop
    Write-Host "Granted database access to $loginName"
    
    Write-Host "Successfully configured SQL permissions for IIS Express"
} catch {
    Write-Host "Error: $_"
    Write-Host "Trying alternative approach..."
    
    # If login already exists, just grant database access
    try {
        $useDatabaseQuery = "USE [$databaseName];"
        $createUserQuery = "CREATE USER [$loginName] FOR LOGIN [$loginName];"
        $grantRoleQuery = "ALTER ROLE db_owner ADD MEMBER [$loginName];"
        
        Invoke-Sqlcmd -ServerInstance $serverInstance -Query "$useDatabaseQuery $createUserQuery $grantRoleQuery" -ErrorAction Stop
        Write-Host "Successfully granted database access to existing login"
    } catch {
        Write-Host "Final error: $_"
    }
}
