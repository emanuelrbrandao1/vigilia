$ErrorActionPreference='Stop'
$desktop=[Environment]::GetFolderPath('Desktop')
$shell=New-Object -ComObject WScript.Shell
$base=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach($folder in @($desktop,$base)){
    $limits=$shell.CreateShortcut((Join-Path $folder 'Foco local.lnk'))
    $limits.TargetPath=Join-Path $PSScriptRoot 'FocoLocal.exe'
    $limits.WorkingDirectory=$PSScriptRoot
    $limits.Description='Vigilia - Foco local: regras, horarios, cotas e pausas'
    $limits.IconLocation=(Join-Path $PSScriptRoot 'assets\foco.ico')+',0'
    $limits.Save()
    $permanent=$shell.CreateShortcut((Join-Path $folder 'Bloqueio permanente.lnk'))
    $permanent.TargetPath=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $permanent.Arguments='-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File "'+(Join-Path $base 'Bloqueador.ps1')+'"'
    $permanent.WorkingDirectory=$base
    $permanent.Description='Bloqueio permanente no Windows, independente da extensao'
    $permanent.IconLocation=(Join-Path $PSScriptRoot 'assets\permanente.ico')+',0'
    $permanent.Save()
}
Write-Output ('Atalhos criados e atualizados em: '+$desktop)
