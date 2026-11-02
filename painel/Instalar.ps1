$ErrorActionPreference='Stop'
$manifest=Join-Path $PSScriptRoot 'native-host.json'
foreach($required in @('native-host.json','FocoLocal.exe','FocoHost.exe')) {
    if(!(Test-Path -LiteralPath (Join-Path $PSScriptRoot $required) -PathType Leaf)){throw 'Execute Compilar.ps1 sem -PrepararAtualizacao antes de instalar.'}
}
$connector=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
$expectedHost=Join-Path $PSScriptRoot 'FocoHost.exe'
if($connector.name -ne 'br.local.foco' -or $connector.path -ne $expectedHost){throw 'Manifesto de outra pasta. Execute Compilar.ps1 nesta pasta antes de instalar.'}
# Register only this connector, for the signed-in user, in both browsers.
foreach($browser in @('Google\Chrome','Microsoft\Edge')){
    $key='HKCU:\Software\'+$browser+'\NativeMessagingHosts\br.local.foco'
    New-Item -Path $key -Force | Out-Null
    Set-Item -LiteralPath $key -Value $manifest
}
$data=Join-Path $PSScriptRoot 'dados.json'
if(!(Test-Path -LiteralPath $data)){
    # Permanent blocks and usage limits have independent lists.
    $rules=@()
    @{version=1;rules=@($rules);day='';usage=@{};seen=@{};ready=@{};chargedUntil=0} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $data -Encoding UTF8
}
& (Join-Path $PSScriptRoot 'Criar-Atalhos.ps1')
