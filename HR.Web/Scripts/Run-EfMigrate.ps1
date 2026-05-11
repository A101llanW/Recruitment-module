# Optional: applies pending EF6 code-based migrations via DbMigrator (uses __MigrationHistory).
# If EF is disabled, use scripts\Apply-MigrationsSql.ps1 instead (see ENTITY_FRAMEWORK_MIGRATIONS.txt).
$ErrorActionPreference = 'Stop'
# PSScriptRoot = ...\HR.Web\scripts
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'bin'
$webConfig = Join-Path $root 'Web.config'
if (-not (Test-Path $webConfig)) {
    throw "Web.config not found at $webConfig"
}
[System.AppDomain]::CurrentDomain.SetData('APP_CONFIG_FILE', $webConfig)
Add-Type -Path (Join-Path $bin 'EntityFramework.dll')
$hr = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'HR.Web.dll'))
$cfgType = $hr.GetType('HR.Web.Migrations.Configuration')
if ($null -eq $cfgType) {
    throw 'HR.Web.Migrations.Configuration not found. Build HR.Web first.'
}
$config = [Activator]::CreateInstance($cfgType)
$migratorType = [System.Data.Entity.Migrations.DbMigrator]
$migrator = [Activator]::CreateInstance($migratorType, @($config))
try {
    $migrator.Update()
    Write-Host 'Entity Framework migrations applied successfully.'
}
catch {
    Write-Host $_.Exception.Message
    Write-Host ''
    Write-Host 'See scripts/ENTITY_FRAMEWORK_MIGRATIONS.txt for the Visual Studio fix (Add-Migration ... -IgnoreChanges).'
    exit 1
}
