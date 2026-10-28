$ErrorActionPreference='Stop'
$manifest=Join-Path $PSScriptRoot 'native-host.json'
if(!(Test-Path -LiteralPath $manifest) -or !(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'FocoLocal.exe'))){throw 'Execute Compilar.ps1 primeiro.'}
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
