$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Motor.ps1')
function Assert($condition, $message) { if (-not $condition) { throw $message } }
$domains = @(ConvertTo-Dominios "X.com`nhttps://twitter.com/`nx.com")
Assert ($domains.Count -eq 4) 'Normalizacao e deduplicacao falharam.'
foreach ($bad in @('https://example.com/a', 'https://example.com?q=x', '*.example.com', '127.0.0.1', 'https://user@example.com')) {
    $rejected = $false
    try { ConvertTo-Dominios $bad | Out-Null } catch { $rejected = $true }
    Assert $rejected "Entrada incorreta aceita: $bad"
}
$original = "# Entrada de outro aplicativo`r`n127.0.0.1 localhost`r`n10.0.0.5 intranet.exemplo`r`n"
$once = New-HostsText -Original $original -Dominios $domains
Assert ((New-HostsText -Original $once -Dominios $domains) -ceq $once) 'Aplicacao repetida alterou o resultado.'
Assert ((New-HostsText -Original $once -Dominios @()) -ceq $original) 'Remocao alterou entradas preexistentes.'
Assert ((Get-DominiosAtivos $once).Count -eq 4) 'Leitura do bloco falhou.'
$rejected = $false
try { New-HostsText -Original ($original + $script:InicioBloco + "`r`n") -Dominios $domains | Out-Null } catch { $rejected = $true }
Assert $rejected 'Marcador incompleto nao foi protegido.'
$testDirectory = Join-Path $env:TEMP ('bloqueador-teste-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testDirectory) | Out-Null
$testPath = Join-Path $testDirectory 'hosts-teste'
$testBackup = Join-Path $testDirectory 'backups'
[IO.File]::WriteAllText($testPath, $original, (New-Object Text.UTF8Encoding($false)))
Set-BloqueioLocal -HostsPath $testPath -Dominios $domains -BackupDirectory $testBackup
Assert ((Get-DominiosAtivos ([IO.File]::ReadAllText($testPath))).Count -eq 4) 'Escrita em arquivo falhou.'
Set-BloqueioLocal -HostsPath $testPath -Dominios @() -BackupDirectory $testBackup
Assert ([IO.File]::ReadAllText($testPath) -ceq $original) 'Restauracao em arquivo falhou.'
Assert ((Get-ChildItem -LiteralPath $testBackup -File).Count -eq 2) 'Backups nao foram criados.'
Write-Output 'Testes passaram: dominios, entradas invalidas, repeticao, preservacao, remocao, marcadores e backups. Arquivo hosts real nao alterado.'
