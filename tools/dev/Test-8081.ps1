$log = 'c:\Users\allan\Documents\Examples\Recruitment\tools\dev\fix-8081-last-run.txt'
Import-Module WebAdministration
"Pool: $((Get-WebAppPoolState Hirehub_Pool).Value)" | Add-Content $log
Restart-WebAppPool Hirehub_Pool
Start-Sleep 5
try {
  $r = Invoke-WebRequest -Uri 'http://localhost:8081/' -UseBasicParsing -TimeoutSec 30
  "HTTP $($r.StatusCode)" | Add-Content $log
} catch {
  "HTTP failed: $($_.Exception.Message)" | Add-Content $log
}
