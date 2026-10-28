$ErrorActionPreference='Stop'
$destination=Join-Path $PSScriptRoot 'FocoLocal.exe'
$staged=Join-Path $PSScriptRoot 'FocoLocal.next.exe'
if(!(Test-Path -LiteralPath $staged)){throw 'Compilacao atualizada nao encontrada.'}
$hostsPath=Join-Path $env:WINDIR 'System32\drivers\etc\hosts'
$before=(Get-FileHash -LiteralPath $hostsPath -Algorithm SHA256).Hash
foreach($running in @(Get-Process FocoLocal -ErrorAction SilentlyContinue)){
    if($running.Path -eq $destination){
        if(!$running.CloseMainWindow()){throw 'Feche o painel para concluir a atualizacao.'}
        if(!$running.WaitForExit(5000)){throw 'O painel ainda esta aberto. Nenhum processo foi encerrado a forca.'}
    }
}
Copy-Item -LiteralPath $destination -Destination (Join-Path $PSScriptRoot 'FocoLocal.anterior.bak') -Force
$dataPath=Join-Path $PSScriptRoot 'dados.json'
$dataBackup=Join-Path $PSScriptRoot ('dados.antes-cores-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json')
if((Test-Path -LiteralPath $dataPath) -and !(Test-Path -LiteralPath $dataBackup)) { Copy-Item -LiteralPath $dataPath -Destination $dataBackup }
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'FocoHost.next.exe')){
    $nativePath=Join-Path $PSScriptRoot 'FocoHost.exe'
    foreach($nativeProcess in @(Get-Process FocoHost -ErrorAction SilentlyContinue)){
        if($nativeProcess.Path -eq $nativePath){Stop-Process -Id $nativeProcess.Id; $nativeProcess.WaitForExit()}
    }
    Copy-Item -LiteralPath $nativePath -Destination (Join-Path $PSScriptRoot 'FocoHost.anterior.bak') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FocoHost.next.exe') -Destination $nativePath -Force
}
Copy-Item -LiteralPath $staged -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app.config') -Destination ($destination+'.config') -Force
& (Join-Path $PSScriptRoot 'Criar-Atalhos.ps1')
$after=(Get-FileHash -LiteralPath $hostsPath -Algorithm SHA256).Hash
if($before -ne $after){throw 'O arquivo hosts mudou durante a atualizacao. Verifique os bloqueios.'}
Write-Output 'Painel atualizado. Arquivo hosts preservado byte a byte.'
Start-Process -FilePath $destination -WindowStyle Normal
