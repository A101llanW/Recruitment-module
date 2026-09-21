$log = 'c:\Users\allan\Documents\Examples\Recruitment\tools\dev\fix-8081-last-run.txt'
Import-Module WebAdministration
try {
  Start-Website -Name Hirehub -ErrorAction Stop
  'Start-Website: OK' | Add-Content $log
} catch {
  "Start-Website failed: $($_.Exception.Message)" | Add-Content $log
}
Start-Sleep 2
(Get-Website -Name Hirehub).State | Add-Content $log
Get-NetTCPConnection -LocalPort 8081,8080 -State Listen -ErrorAction SilentlyContinue | ForEach-Object { "$($_.LocalAddress):$($_.LocalPort)" } | Add-Content $log
