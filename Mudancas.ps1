function Get-HostsChangeDecision {
    param([string[]]$Current,[string[]]$Target,$Pending,[DateTimeOffset]$Now=[DateTimeOffset]::UtcNow)
    $currentSorted=@($Current | Sort-Object -Unique)
    $targetSorted=@($Target | Sort-Object -Unique)
    $removes=@($currentSorted | Where-Object { $_ -notin $targetSorted }).Count -gt 0
    if(!$removes){return [pscustomobject]@{Apply=$true;Pending=$null}}
    $same=$Pending -and (($Pending.Target -join '|') -ceq ($targetSorted -join '|')) -and (($Pending.Base -join '|') -ceq ($currentSorted -join '|'))
    if($same -and $Now.ToUnixTimeSeconds() -ge [double]$Pending.ExecuteAt){return [pscustomobject]@{Apply=$true;Pending=$null}}
    if(!$same){$Pending=[pscustomobject]@{Base=$currentSorted;Target=$targetSorted;ExecuteAt=$Now.AddHours(24).ToUnixTimeSeconds()}}
    return [pscustomobject]@{Apply=$false;Pending=$Pending}
}
