# Grant SQL Server database access to IIS App Pool
$ErrorActionPreference = "Continue"

Write-Host "=== Granting Database Access ===" -ForegroundColor Cyan
Write-Host ""

$connectionString = "Data Source=.\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;Connection Timeout=30;"
$dbConnectionString = "Data Source=.\SQLEXPRESS;Initial Catalog=HR_Local;Integrated Security=True;Connection Timeout=30;"

try {
    $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    Write-Host "Current User: $currentUser" -ForegroundColor Yellow
    Write-Host ""
    
    $masterConn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $masterConn.Open()
    Write-Host "Connected to SQL Server" -ForegroundColor Green
    
    $dbConn = New-Object System.Data.SqlClient.SqlConnection($dbConnectionString)
    $dbConn.Open()
    Write-Host "Connected to HR_Local database" -ForegroundColor Green
    Write-Host ""
    
    # Grant access to current user
    Write-Host "Granting access to current user..." -ForegroundColor Yellow
    try {
        $cmd = $masterConn.CreateCommand()
        $cmd.CommandText = "IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = '$currentUser') CREATE LOGIN [$currentUser] FROM WINDOWS"
        $cmd.ExecuteNonQuery()
        
        $cmd = $dbConn.CreateCommand()
        $cmd.CommandText = "IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '$currentUser') CREATE USER [$currentUser] FOR LOGIN [$currentUser]"
        $cmd.ExecuteNonQuery()
        
        $cmd.CommandText = "IF IS_MEMBER('db_owner') = 0 ALTER ROLE db_owner ADD MEMBER [$currentUser]"
        $cmd.ExecuteNonQuery()
        Write-Host "  Granted access to $currentUser" -ForegroundColor Green
    } catch {
        Write-Host "  Warning: $_" -ForegroundColor Yellow
    }
    
    # Grant access to IIS_IUSRS
    Write-Host "Granting access to IIS_IUSRS..." -ForegroundColor Yellow
    try {
        $cmd = $masterConn.CreateCommand()
        $cmd.CommandText = "IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'IIS_IUSRS') CREATE LOGIN [IIS_IUSRS] FROM WINDOWS"
        $cmd.ExecuteNonQuery()
        
        $cmd = $dbConn.CreateCommand()
        $cmd.CommandText = "IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'IIS_IUSRS') CREATE USER [IIS_IUSRS] FOR LOGIN [IIS_IUSRS]"
        $cmd.ExecuteNonQuery()
        
        $cmd.CommandText = "ALTER ROLE db_owner ADD MEMBER [IIS_IUSRS]"
        $cmd.ExecuteNonQuery()
        Write-Host "  Granted access to IIS_IUSRS" -ForegroundColor Green
    } catch {
        Write-Host "  Warning: $_" -ForegroundColor Yellow
    }
    
    # Grant access to BUILTIN\Users
    Write-Host "Granting access to BUILTIN\Users..." -ForegroundColor Yellow
    try {
        $cmd = $masterConn.CreateCommand()
        $cmd.CommandText = "IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'BUILTIN\Users') CREATE LOGIN [BUILTIN\Users] FROM WINDOWS"
        $cmd.ExecuteNonQuery()
        
        $cmd = $dbConn.CreateCommand()
        $cmd.CommandText = "IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'BUILTIN\Users') CREATE USER [BUILTIN\Users] FOR LOGIN [BUILTIN\Users]"
        $cmd.ExecuteNonQuery()
        
        $cmd.CommandText = "ALTER ROLE db_owner ADD MEMBER [BUILTIN\Users]"
        $cmd.ExecuteNonQuery()
        Write-Host "  Granted access to BUILTIN\Users" -ForegroundColor Green
    } catch {
        Write-Host "  Warning: $_" -ForegroundColor Yellow
    }
    
    # Try App Pool users
    $appPoolUsers = @("IIS APPPOOL\.NET v4.5", "IIS APPPOOL\DefaultAppPool", "IIS APPPOOL\Clr4IntegratedAppPool")
    Write-Host "Attempting to grant access to App Pool users..." -ForegroundColor Yellow
    
    foreach ($appPoolUser in $appPoolUsers) {
        try {
            $cmd = $masterConn.CreateCommand()
            $sql = "IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = '$appPoolUser') EXEC('CREATE LOGIN [$appPoolUser] FROM WINDOWS')"
            $cmd.CommandText = $sql
            $cmd.ExecuteNonQuery()
            
            $cmd = $dbConn.CreateCommand()
            $sql = "IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '$appPoolUser') EXEC('CREATE USER [$appPoolUser] FOR LOGIN [$appPoolUser]')"
            $cmd.CommandText = $sql
            $cmd.ExecuteNonQuery()
            
            $sql = "EXEC('ALTER ROLE db_owner ADD MEMBER [$appPoolUser]')"
            $cmd.CommandText = $sql
            $cmd.ExecuteNonQuery()
            Write-Host "  Granted access to $appPoolUser" -ForegroundColor Green
        } catch {
            # Ignore errors for app pool users that don't exist
        }
    }
    
    $masterConn.Close()
    $dbConn.Close()
    
    Write-Host ""
    Write-Host "=== Configuration Complete ===" -ForegroundColor Green
    Write-Host "Please restart IIS Express for changes to take effect." -ForegroundColor Yellow
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternative: Use SQL Authentication in Web.config" -ForegroundColor Yellow
    exit 1
}
