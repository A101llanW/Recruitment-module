# Working migration script using EF tools
$toolsPath = "C:\Users\allan\Documents\Examples\HR\packages\EntityFramework.6.4.4\tools"
$projectPath = "C:\Users\allan\Documents\Examples\HR\HR.Web"

# Import EF module
Import-Module "$toolsPath\EntityFramework6.psm1"

# Change to project directory
Set-Location $projectPath

# Run database update
Update-Database -ProjectName HR.Web -Verbose

Write-Host "Migration completed!"
