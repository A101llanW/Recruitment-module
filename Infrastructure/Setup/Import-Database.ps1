# Import HR_Local database
$backupPath = "C:\temp\HR_Local.bak"
$serverName = "(localdb)\MSSQLLocalDB"
$databaseName = "HR_Local"

# Check if backup file exists
if (!(Test-Path $backupPath)) {
    Write-Error "Backup file not found at: $backupPath"
    Write-Host "Please copy the backup file to this location first."
    exit 1
}

try {
    Write-Host "Restoring database $databaseName to $serverName..."
    
    # Kill existing connections if database exists
    $killConnections = @"
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = '$databaseName')
BEGIN
    ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
END
"@
    
    sqlcmd -S "$serverName" -Q "$killConnections"
    
    # Restore database
    $restoreQuery = @"
RESTORE DATABASE [$databaseName] 
FROM DISK = '$backupPath'
WITH REPLACE, RECOVERY,
MOVE 'HR_Local' TO 'C:\Users\$($env:USERNAME)\HR_Local.mdf',
MOVE 'HR_Local_log' TO 'C:\Users\$($env:USERNAME)\HR_Local_log.ldf'
"@
    
    sqlcmd -S "$serverName" -Q "$restoreQuery"
    
    # Set back to multi-user
    sqlcmd -S "$serverName" -Q "ALTER DATABASE [$databaseName] SET MULTI_USER"
    
    Write-Host "Database restored successfully!"
    Write-Host "You can now connect to $databaseName on $serverName"
}
catch {
    Write-Error "Restore failed: $_"
}
