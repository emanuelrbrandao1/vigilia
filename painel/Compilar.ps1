param([switch]$PrepararAtualizacao)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
    & node (Join-Path $PSScriptRoot 'build.js')
    if($LASTEXITCODE -ne 0){throw 'Falha ao preparar a extensao.'}
    & $compiler /nologo /target:exe /out:Tests.exe /r:System.Web.Extensions.dll Core.cs Changes.cs GuardCore.cs ChangeTests.cs Tests.cs
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar os testes.'}
    & .\Tests.exe
    if($LASTEXITCODE -ne 0){throw 'Os testes do motor falharam.'}
    & $compiler /nologo /target:exe /out:MakeAssets.exe /r:System.Drawing.dll Artwork.cs MakeAssets.cs
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar os icones.'}
    & .\MakeAssets.exe
    if($LASTEXITCODE -ne 0){throw 'Falha ao gerar os icones.'}
    $hostOutput=if($PrepararAtualizacao){'FocoHost.next.exe'}else{'FocoHost.exe'}
    $uiOutput=if($PrepararAtualizacao){'FocoLocal.next.exe'}else{'FocoLocal.exe'}
    & $compiler /nologo /target:exe ('/out:'+$hostOutput) /r:System.Web.Extensions.dll Core.cs Changes.cs NativeHost.cs
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o conector.'}
    & $compiler /nologo /target:exe /out:FocoGuard.exe /r:System.Web.Extensions.dll /r:System.ServiceProcess.dll Core.cs Changes.cs GuardCore.cs GuardService.cs
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o servico de restauracao.'}
    & $compiler /nologo /target:winexe ('/out:'+$uiOutput) /win32icon:assets/foco.ico /win32manifest:app.manifest /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll Core.cs Changes.cs GuardCore.cs Artwork.cs Theme.cs Painel.cs Dashboard.cs FocusDialog.cs PauseDialog.cs ChangesDialog.cs
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o painel.'}
    Copy-Item -LiteralPath 'app.config' -Destination ($uiOutput+'.config') -Force
} finally {Pop-Location}
