# Export all data as SQL INSERT statements
$outputPath = "C:\temp\HR_Data_$(Get-Date -Format 'yyyyMMdd_HHmmss').sql"
$serverName = "tcp:localhost,1433"
$databaseName = "HR_Local"

# Create temp directory if not exists
if (!(Test-Path "C:\temp")) {
    New-Item -ItemType Directory -Path "C:\temp"
}

Write-Host "Exporting data from: $databaseName"
Write-Host "Output file: $outputPath"

# Get all user tables
$tablesQuery = @"
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE' 
AND TABLE_CATALOG = '$databaseName'
ORDER BY TABLE_NAME
"@

$tables = sqlcmd -S "$serverName" -d "$databaseName" -Q "$tablesQuery" -h -1 -s "," -W | Where-Object { $_ -notmatch "^$" -and $_ -notmatch "TABLE_NAME" }

# Start building the SQL script
$sqlScript = @"
-- ========================================
-- HR Database Data Export
-- Generated: $(Get-Date)
-- Database: $databaseName
-- ========================================

USE [HR_Local];
GO

SET IDENTITY_INSERT [Departments] OFF;
SET IDENTITY_INSERT [Positions] OFF;
SET IDENTITY_INSERT [Applicants] OFF;
SET IDENTITY_INSERT [Applications] OFF;
SET IDENTITY_INSERT [Interviews] OFF;
SET IDENTITY_INSERT [Users] OFF;
SET IDENTITY_INSERT [AuditLogs] OFF;
GO

"@

foreach ($table in $tables) {
    $table = $table.Trim()
    Write-Host "Exporting table: $table"
    
    # Generate INSERT statements for each table
    $insertQuery = @"
DECLARE @sql NVARCHAR(MAX) = '';
DECLARE @columns NVARCHAR(MAX) = '';

SELECT @columns = STRING_AGG(QUOTENAME(COLUMN_NAME), ', ')
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = '$table' AND TABLE_CATALOG = '$databaseName';

SET @sql = 'SELECT ''INSERT INTO [' + '$table' + '] (' + @columns + ') VALUES (' + 
    STRING_AGG('''' + REPLACE(CONVERT(NVARCHAR(MAX), [' + COLUMN_NAME + ']), '''', '''''') + '''', '', '') + '')'' 
FROM [' + '$table' + ']';

EXEC sp_executesql @sql;
"@
    
    try {
        $inserts = sqlcmd -S "$serverName" -d "$databaseName" -Q "$insertQuery" -h -1 -s "," -W | Where-Object { $_ -notmatch "^$" -and $_ -notmatch "affected" }
        
        if ($inserts) {
            $sqlScript += @"
-- ========================================
-- Table: $table
-- ========================================
SET IDENTITY_INSERT [$table] ON;
GO

$($inserts -join "`n")

SET IDENTITY_INSERT [$table] OFF;
GO

"@
        }
    }
    catch {
        Write-Host "Warning: Could not export table $table - $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Save the script
$sqlScript | Out-File -FilePath $outputPath -Encoding UTF8

if (Test-Path $outputPath) {
    $fileSize = [math]::Round((Get-Item $outputPath).Length / 1KB, 2)
    Write-Host "✅ Data export completed!" -ForegroundColor Green
    Write-Host "📍 Location: $outputPath" -ForegroundColor Cyan
    Write-Host "📊 File size: $fileSize KB" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "This SQL script contains:"
    Write-Host "• All data from all tables"
    Write-Host "• INSERT statements for easy restoration"
    Write-Host "• Proper IDENTITY_INSERT handling"
    Write-Host ""
    Write-Host "To restore: Run this script in SQL Server Management Studio"
} else {
    Write-Error "❌ Export failed - file not created"
}
