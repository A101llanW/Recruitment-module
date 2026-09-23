$log = 'c:\Users\allan\Documents\Examples\Recruitment\tools\dev\fix-8081-last-run.txt'
Import-Module WebAdministration
'All sites:' | Add-Content $log
Get-Website | ForEach-Object {
  $name = $_.Name
  $state = $_.State
  $bindings = (Get-WebBinding -Name $name | ForEach-Object { $_.bindingInformation }) -join ', '
  "$name [$state] -> $bindings" | Add-Content $log
}
'HTTP.sys:' | Add-Content $log
(netsh http show servicestate | Select-String '8081|8080|Registered URL' -Context 0,1 | Out-String) | Add-Content $log
'URL ACL:' | Add-Content $log
(netsh http show urlacl | Select-String '8081' | Out-String) | Add-Content $log
