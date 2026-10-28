$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Mudancas.ps1')
$now=[DateTimeOffset]'2026-10-23T12:00:00-03:00'
function Assert($Value,$Name){if(!$Value){throw "FAIL: $Name"};Write-Output "OK: $Name"}
$queued=Get-HostsChangeDecision -Current @('a.test','b.test') -Target @('a.test') -Now $now
Assert (!$queued.Apply -and $queued.Pending.ExecuteAt -eq $now.AddHours(24).ToUnixTimeSeconds()) 'Windows removals wait 24 hours'
$early=Get-HostsChangeDecision -Current @('a.test','b.test') -Target @('a.test') -Pending $queued.Pending -Now $now.AddHours(23)
Assert (!$early.Apply -and $early.Pending.ExecuteAt -eq $queued.Pending.ExecuteAt) 'Repeat keeps deadline'
$due=Get-HostsChangeDecision -Current @('a.test','b.test') -Target @('a.test') -Pending $queued.Pending -Now $now.AddHours(24)
Assert $due.Apply 'Exact deadline releases requested removal'
$strict=Get-HostsChangeDecision -Current @('a.test','b.test') -Target @('a.test','b.test','c.test') -Pending $queued.Pending -Now $now
Assert ($strict.Apply -and !$strict.Pending) 'Additional blocks immediate and cancel old request'
$changed=Get-HostsChangeDecision -Current @('a.test','b.test') -Target @() -Pending $queued.Pending -Now $now.AddHours(1)
Assert (!$changed.Apply -and $changed.Pending.ExecuteAt -eq $now.AddHours(25).ToUnixTimeSeconds()) 'Different removal restarts waiting'
$changedBase=Get-HostsChangeDecision -Current @('a.test','b.test','c.test') -Target @('a.test') -Pending $queued.Pending -Now $now.AddHours(24)
Assert (!$changedBase.Apply) 'Old request cannot undo a newer restriction'
Assert ((Get-HostsChangeDecision -Current @() -Target @('a.test') -Now $now).Apply) 'First Windows block immediate'
