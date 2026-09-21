$log = 'c:\Users\allan\Documents\Examples\Recruitment\tools\dev\fix-8081-last-run.txt'
"FIX3 $(Get-Date -Format o)" | Add-Content $log
Import-Module WebAdministration

# Hirehub cannot start while it shares *:8080 with AssetManagement
$hirehub8080 = Get-WebBinding -Name Hirehub -Protocol http | Where-Object { $_.bindingInformation -eq '*:8080:' }
if ($hirehub8080) {
  Remove-WebBinding -Name Hirehub -BindingInformation '*:8080:' -Protocol http
  'Removed duplicate *:8080 from Hirehub' | Add-Content $log
}

try {
  Start-Website -Name Hirehub -ErrorAction Stop
  'Start-Website: OK' | Add-Content $log
} catch {
  "Start-Website failed: $($_.Exception.Message)" | Add-Content $log
}

Start-Sleep 2
"State: $((Get-Website -Name Hirehub).State)" | Add-Content $log
Get-WebBinding -Name Hirehub | ForEach-Object { $_.bindingInformation } | Add-Content $log
Get-NetTCPConnection -LocalPort 8081,8080,5002 -State Listen -ErrorAction SilentlyContinue | ForEach-Object { "$($_.LocalAddress):$($_.LocalPort)" } | Add-Content $log
