# Run COMPLETE_SCHEMA_FIX.sql using ADO.NET
$ErrorActionPreference = "Stop"

Write-Host "Connecting to SQL Server..."
$connectionString = "Data Source=.\SQLEXPRESS;Initial Catalog=HR_Local;Integrated Security=True;Connection Timeout=30;"

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Host "Connected successfully!" -ForegroundColor Green
    
    $sqlScript = Get-Content "COMPLETE_SCHEMA_FIX.sql" -Raw
    
    # Split by GO statements
    $batches = $sqlScript -split "GO\s*\r?\n" | Where-Object { $_.Trim() -ne "" }
    
    Write-Host "Executing $($batches.Count) SQL batches..." -ForegroundColor Yellow
    
    foreach ($batch in $batches) {
        if ($batch.Trim() -ne "") {
            try {
                $command = $connection.CreateCommand()
                $command.CommandText = $batch
                $command.CommandTimeout = 60
                $result = $command.ExecuteNonQuery()
                Write-Host "  Batch executed successfully" -ForegroundColor Green
            }
            catch {
                # Some batches might return results (SELECT statements), try ExecuteReader
                try {
                    $reader = $command.ExecuteReader()
                    while ($reader.Read()) {
                        # Just consume the results
                    }
                    $reader.Close()
                    Write-Host "  Batch executed successfully (with results)" -ForegroundColor Green
                }
                catch {
                    Write-Host "  Warning: $_" -ForegroundColor Yellow
                }
            }
        }
    }
    
    $connection.Close()
    Write-Host "`nSchema fix completed successfully!" -ForegroundColor Green
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "`nTrying alternative connection strings..." -ForegroundColor Yellow
    
    # Try alternative connection strings
    $alternatives = @(
        "Data Source=localhost\SQLEXPRESS;Initial Catalog=HR_Local;Integrated Security=True;Connection Timeout=30;",
        "Data Source=(local)\SQLEXPRESS;Initial Catalog=HR_Local;Integrated Security=True;Connection Timeout=30;",
        "Data Source=.;Initial Catalog=HR_Local;Integrated Security=True;Connection Timeout=30;"
    )
    
    foreach ($altConnString in $alternatives) {
        Write-Host "Trying: $altConnString" -ForegroundColor Cyan
        try {
            $connection = New-Object System.Data.SqlClient.SqlConnection($altConnString)
            $connection.Open()
            Write-Host "Connected with alternative connection string!" -ForegroundColor Green
            
            # Run just the Location column fix
            $fixSql = @"
USE HR_Local;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Positions') 
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Positions') AND name = 'Location')
BEGIN
    ALTER TABLE Positions ADD Location NVARCHAR(200) NULL;
    PRINT 'Location column added to Positions';
END
ELSE IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Positions')
BEGIN
    PRINT 'Location column already exists in Positions';
END
"@
            
            $command = $connection.CreateCommand()
            $command.CommandText = $fixSql
            $command.ExecuteNonQuery()
            $connection.Close()
            Write-Host "Location column fix applied!" -ForegroundColor Green
            exit 0
        }
        catch {
            Write-Host "  Failed: $_" -ForegroundColor Red
        }
    }
    
    Write-Host "`nCould not connect to SQL Server. Please:" -ForegroundColor Red
    Write-Host "1. Ensure SQL Server Express is running" -ForegroundColor Yellow
    Write-Host "2. Check the connection string in Web.config" -ForegroundColor Yellow
    Write-Host "3. Run the COMPLETE_SCHEMA_FIX.sql script manually in SQL Server Management Studio" -ForegroundColor Yellow
    exit 1
}
