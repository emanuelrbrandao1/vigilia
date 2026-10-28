param(
    [switch]$VerificarInterface,
    [ValidateSet('Aplicar','Remover')][string]$Acao,
    [string]$Entrada,
    [string]$Resultado
)
$ErrorActionPreference = 'Stop'
$startupLog = Join-Path $PSScriptRoot 'abertura.log'
function Write-StartupLog([string]$Message) {
    Add-Content -LiteralPath $startupLog -Value ((Get-Date -Format 's') + ' ' + $Message) -Encoding UTF8
}
trap {
    ($_ | Out-String) + $_.ScriptStackTrace | Add-Content -LiteralPath $startupLog -Encoding UTF8
    try { [Windows.Forms.MessageBox]::Show("Nao foi possivel abrir o bloqueador. Detalhes em:`r`n$startupLog`r`n`r`n$($_.Exception.Message)", 'Erro ao abrir') | Out-Null } catch { }
    exit 1
}
Write-StartupLog "Inicio PID=$PID VerificarInterface=$VerificarInterface"
. (Join-Path $PSScriptRoot 'Motor.ps1')
. (Join-Path $PSScriptRoot 'Mudancas.ps1')
$pendingPath=Join-Path $PSScriptRoot 'mudanca-windows-pendente.json'
$hostsPath = Join-Path $env:WINDIR 'System32\drivers\etc\hosts'
$backupDirectory = Join-Path $PSScriptRoot 'backups'
if ($Acao) {
    $changeGate=New-Object Threading.Mutex($false,'Local\FocoLocalHostsChanges_v1')
    $changeHeld=$false
    try {
        try {$changeHeld=$changeGate.WaitOne(5000)}catch [Threading.AbandonedMutexException]{$changeHeld=$true}
        if(!$changeHeld){throw 'Outra alteracao esta em andamento.'}
        $domains = @()
        if ($Acao -eq 'Aplicar') {
            $domains = @(ConvertTo-Dominios ([IO.File]::ReadAllText($Entrada)))
            if (-not $domains.Count) { throw 'Nenhum dominio informado.' }
        }
        $current=@(Get-DominiosAtivos ([IO.File]::ReadAllText($hostsPath)))
        $pending=if(Test-Path -LiteralPath $pendingPath){Get-Content -Raw -LiteralPath $pendingPath | ConvertFrom-Json}else{$null}
        $decision=Get-HostsChangeDecision -Current $current -Target $domains -Pending $pending
        if(!$decision.Apply){
            [IO.File]::WriteAllText($pendingPath,($decision.Pending | ConvertTo-Json -Depth 5))
            @{Success=$true;PendingAt=$decision.Pending.ExecuteAt} | ConvertTo-Json | Set-Content -LiteralPath $Resultado -Encoding UTF8
            exit
        }
        Set-BloqueioLocal -HostsPath $hostsPath -Dominios $domains -BackupDirectory $backupDirectory
        if(Test-Path -LiteralPath $pendingPath){Remove-Item -LiteralPath $pendingPath}
        Clear-DnsClientCache -ErrorAction SilentlyContinue
        @{Success=$true} | ConvertTo-Json | Set-Content -LiteralPath $Resultado -Encoding UTF8
    } catch {
        @{Success=$false; Error=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath $Resultado -Encoding UTF8
    } finally {
        if($changeHeld){$changeGate.ReleaseMutex()};$changeGate.Dispose()
    }
    exit
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()

$appScriptPath = $PSCommandPath
function Invoke-Alteracao([string]$Action, [string]$Sites) {
    $jobDirectory = Join-Path $PSScriptRoot ('operacoes\' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($jobDirectory) | Out-Null
    $inputPath = Join-Path $jobDirectory 'sites.txt'
    $resultPath = Join-Path $jobDirectory 'resultado.json'
    [IO.File]::WriteAllText($inputPath, $Sites)
    $argsList = @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $appScriptPath + '"'),'-Acao',$Action,'-Entrada',('"' + $inputPath + '"'),'-Resultado',('"' + $resultPath + '"'))
    Start-Process -FilePath "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -Verb RunAs -WindowStyle Hidden -ArgumentList $argsList -Wait | Out-Null
    if (-not (Test-Path -LiteralPath $resultPath)) { throw 'A alteracao nao foi confirmada. Verifique se autorizou o aviso do Windows.' }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if (-not $result.Success) { throw $result.Error }
    return $result
}
$form = New-Object Windows.Forms.Form
$form.Text = 'Vigilia | Bloqueio permanente'
$iconPath = Join-Path $PSScriptRoot 'painel\assets\permanente.ico'
if (Test-Path -LiteralPath $iconPath) { $form.Icon = New-Object Drawing.Icon($iconPath) }
$form.ClientSize = New-Object Drawing.Size(640, 600)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.ForeColor = [Drawing.Color]::FromArgb(242,241,238)
$form.Font = New-Object Drawing.Font('Segoe UI', 10)
$form.BackColor = [Drawing.Color]::FromArgb(17,18,21)

$title = New-Object Windows.Forms.Label
$title.Text = 'Bloqueio permanente no Windows'
$title.Font = New-Object Drawing.Font('Segoe UI', 17, [Drawing.FontStyle]::Bold)
$title.SetBounds(24, 22, 590, 38)
$form.Controls.Add($title)
$description = New-Object Windows.Forms.Label
$description.Text = 'Sem horarios e sem extensao. Um dominio por linha; www e incluido. Estes bloqueios prevalecem sobre os limites do Foco local.'
$description.SetBounds(26, 68, 585, 45)
$form.Controls.Add($description)

$inputSites = New-Object Windows.Forms.TextBox
$inputSites.Multiline = $true
$inputSites.BackColor = [Drawing.Color]::FromArgb(27,28,32)
$inputSites.ForeColor = [Drawing.Color]::FromArgb(242,241,238)
$inputSites.AcceptsReturn = $true
$inputSites.ScrollBars = 'Vertical'
$inputSites.Font = New-Object Drawing.Font('Consolas', 11)
$inputSites.SetBounds(26, 120, 588, 240)
$form.Controls.Add($inputSites)
$status = New-Object Windows.Forms.Label
$status.SetBounds(26, 375, 588, 42)
$form.Controls.Add($status)

$apply = New-Object Windows.Forms.Button
$apply.Text = 'Aplicar bloqueio'
$apply.SetBounds(26, 426, 240, 45)
$apply.BackColor = [Drawing.Color]::FromArgb(242,241,238)
$apply.ForeColor = [Drawing.Color]::FromArgb(17,18,21)
$apply.FlatStyle = 'Flat'
$form.Controls.Add($apply)
$remove = New-Object Windows.Forms.Button
$remove.Text = 'Remover bloqueios permanentes'
$remove.FlatStyle = 'Flat'
$remove.BackColor = [Drawing.Color]::FromArgb(27,28,32)
$remove.SetBounds(284, 426, 330, 45)
$form.Controls.Add($remove)
$note = New-Object Windows.Forms.Label
$note.Text = "Adicionar bloqueios e imediato. Remover enderecos exige 24 h; depois, aplique novamente a mesma lista e autorize o Windows. Um pedido diferente reinicia a espera. Adicionar restricoes cancela o pedido antigo. Nao protege contra alteracoes manuais de administrador."
$note.SetBounds(26, 490, 588, 95)
$note.ForeColor = [Drawing.Color]::FromArgb(173,175,182)
$form.Controls.Add($note)

function Update-Status {
    $active = @(Get-DominiosAtivos ([IO.File]::ReadAllText($hostsPath)))
    if ($active.Count) { $status.Text = "Bloqueio ativo: $($active.Count) enderecos (incluindo www)." }
    else { $status.Text = 'Nenhum bloqueio deste prototipo aplicado ainda.' }
    if(Test-Path -LiteralPath $pendingPath){$pending=Get-Content -Raw -LiteralPath $pendingPath | ConvertFrom-Json;if($pending){$date=[DateTimeOffset]::FromUnixTimeSeconds([long]$pending.ExecuteAt).LocalDateTime;$status.Text+="`r`nRemocao solicitada: reaplique a lista apos $($date.ToString('dd/MM HH:mm'))."}}
    $remove.Enabled = $active.Count -gt 0
}
try {
    $active = @(Get-DominiosAtivos ([IO.File]::ReadAllText($hostsPath)))
    if ($active.Count) { $inputSites.Lines = $active }
    else { $inputSites.Lines = @('x.com','twitter.com','mobile.twitter.com','tiktok.com','m.tiktok.com','vm.tiktok.com','vt.tiktok.com') }
    Update-Status
} catch { $status.Text = $_.Exception.Message; $apply.Enabled = $false; $remove.Enabled = $false }

$apply.Add_Click({
    try {
        $domains = @(ConvertTo-Dominios $inputSites.Text)
        if (-not $domains.Count) { throw 'Adicione pelo menos um site ou use o botao de remover.' }
        $result=Invoke-Alteracao -Action 'Aplicar' -Sites ($domains -join "`r`n")
        Update-Status
        $message=if($result.PendingAt){'A remocao exige esperar 24 horas. Os bloqueios atuais continuam. Depois do prazo mostrado, aplique novamente a mesma lista.'}else{'Bloqueio aplicado. Pedidos anteriores de remocao foram cancelados. Feche e reabra seu navegador.'}
        [Windows.Forms.MessageBox]::Show($message, 'Concluido') | Out-Null
    } catch { [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Nao foi possivel aplicar') | Out-Null }
})
$remove.Add_Click({
    try {
        $result=Invoke-Alteracao -Action 'Remover' -Sites ''
        Update-Status
        if($result.PendingAt){[Windows.Forms.MessageBox]::Show('Remocao solicitada. Os bloqueios continuam por 24 horas. Depois do prazo, clique novamente em Remover e confirme o aviso do Windows.','Espera de 24 horas') | Out-Null}
    } catch { [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Nao foi possivel remover') | Out-Null }
})
if ($VerificarInterface) {
    $form.CreateControl()
    if ($form.Controls.Count -ne 7) { throw 'Controles de interface incompletos.' }
    $form.Dispose()
    Write-Output 'Interface criada e verificada, sem alterar o Windows.'
} else {
    $form.Add_Shown({
        # A hidden PowerShell startup can propagate its initial show state to
        # the first form. Show it again after that initial state was consumed.
        $form.Hide()
        $form.Show()
        $form.Activate()
        Write-StartupLog "Janela exibida. Visible=$($form.Visible) Handle=$($form.Handle)"
    })
    [Windows.Forms.Application]::Run($form)
    $form.Dispose()
}
