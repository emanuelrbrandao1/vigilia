$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(!(New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Execute como administrador.'}
$destination=Join-Path $env:ProgramFiles 'FocoLocalGuard'
$stateDirectory=Join-Path $env:ProgramData 'FocoLocalGuard'
$account=(Get-CimInstance Win32_ComputerSystem).UserName
if(!$account){throw 'Usuario interativo nao encontrado.'}
$userSid=(New-Object Security.Principal.NTAccount($account)).Translate([Security.Principal.SecurityIdentifier]).Value
$existingService=Get-CimInstance Win32_Service -Filter "Name='FocoLocalGuard'"
if($existingService -and $existingService.PathName.Trim('"') -ne (Join-Path $destination 'FocoGuard.exe')){throw 'Ja existe outro servico com esse nome. Instalacao cancelada.'}
foreach($directory in @($destination,$stateDirectory)) {
    if((Test-Path -LiteralPath $directory) -and ((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Diretorio de instalacao redirecionado; instalacao cancelada.'}
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $acl=New-Object Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true,$false)
    $acl.SetOwner((New-Object Security.Principal.SecurityIdentifier('S-1-5-32-544')))
    foreach($entry in @(@('S-1-5-18','FullControl'),@('S-1-5-32-544','FullControl'),@($userSid,'ReadAndExecute'))) {
        $sid=New-Object Security.Principal.SecurityIdentifier($entry[0])
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($sid,$entry[1],'ContainerInherit,ObjectInherit','None','Allow')))
    }
    Set-Acl -LiteralPath $directory -AclObject $acl
    foreach($item in @(Get-ChildItem -LiteralPath $directory -Force)) {
        if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Item inesperado no diretorio protegido. Instalacao cancelada.'}
        $fileAcl=New-Object Security.AccessControl.FileSecurity
        $fileAcl.SetAccessRuleProtection($false,$false)
        $fileAcl.SetOwner((New-Object Security.Principal.SecurityIdentifier('S-1-5-32-544')))
        Set-Acl -LiteralPath $item.FullName -AclObject $fileAcl
    }
}
try {
    $service=Get-Service FocoLocalGuard -ErrorAction SilentlyContinue
    if($service -and $service.Status -ne 'Stopped'){Stop-Service FocoLocalGuard; $service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(15))}
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FocoGuard.exe') -Destination (Join-Path $destination 'FocoGuard.exe') -Force
    # Resolve the logged-in user, including an elevation using another administrator account.
    [IO.File]::WriteAllText((Join-Path $destination 'user.sid'),$userSid)
    $binary='"'+(Join-Path $destination 'FocoGuard.exe')+'"'
    if(!$service){New-Service -Name FocoLocalGuard -DisplayName 'Foco local - pausas e restauracao de bloqueios' -BinaryPathName $binary -StartupType Automatic | Out-Null}
    & sc.exe failure FocoLocalGuard reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Falha ao configurar recuperacao do servico.'}
    Start-Service FocoLocalGuard
    [IO.File]::WriteAllText((Join-Path $stateDirectory 'installation.txt'),'Instalado em '+[DateTime]::Now.ToString('s'))
} catch {
    [IO.File]::WriteAllText((Join-Path $stateDirectory 'installation-error.txt'),$_.Exception.Message)
    throw
}
