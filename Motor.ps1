$script:InicioBloco = '# BEGIN BLOQUEADOR-SIMPLES-LOCAL'
$script:FimBloco = '# END BLOQUEADOR-SIMPLES-LOCAL'

function ConvertTo-Dominios {
    param([string]$Texto)
    $result = @()
    foreach ($line in ($Texto -split '\r?\n')) {
        $value = $line.Trim()
        if (-not $value -or $value.StartsWith('#')) { continue }
        if ($value -notmatch '^https?://') { $value = 'https://' + $value }
        $uri = $null
        if (-not [Uri]::TryCreate($value, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('http','https') -or $uri.UserInfo) {
            throw "Endereco invalido: $line"
        }
        $domain = $uri.IdnHost.ToLowerInvariant().TrimEnd('.')
        if ($domain.Length -gt 253 -or $domain -notmatch '^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z](?:[a-z0-9-]{0,61}[a-z0-9])?$') {
            throw "Digite um dominio, como exemplo.com: $line"
        }
        if ($uri.AbsolutePath -ne '/' -or $uri.Query -or $uri.Fragment -or -not $uri.IsDefaultPort) {
            throw "Use apenas o dominio, sem caminho, porta ou parametros: $line"
        }
        $result += $domain
        if (-not $domain.StartsWith('www.')) { $result += 'www.' + $domain }
    }
    return @($result | Sort-Object -Unique)
}

function Get-BlocoLocal {
    param([string]$Texto)
    $pattern = '(?ms)^' + [regex]::Escape($script:InicioBloco) + '\r?\n(.*?)^' + [regex]::Escape($script:FimBloco) + '(?:\r?\n|$)'
    $matchesFound = [regex]::Matches($Texto, $pattern)
    $begins = [regex]::Matches($Texto, '(?m)^' + [regex]::Escape($script:InicioBloco) + '\r?$').Count
    $ends = [regex]::Matches($Texto, '(?m)^' + [regex]::Escape($script:FimBloco) + '\r?$').Count
    if ($begins -ne $ends -or $begins -gt 1 -or $matchesFound.Count -ne $begins) {
        throw 'O bloco local foi modificado ou esta incompleto. Nenhuma alteracao foi feita.'
    }
    return [pscustomobject]@{ Pattern=$pattern; Matches=$matchesFound }
}

function New-HostsText {
    param([string]$Original, [string[]]$Dominios)
    $block = Get-BlocoLocal $Original
    $clean = [regex]::Replace($Original, $block.Pattern, '')
    if ($Dominios.Count -eq 0) { return $clean }
    $newline = if ($Original.Contains("`r`n")) { "`r`n" } else { "`n" }
    if ($clean.Length -gt 0 -and -not $clean.EndsWith("`n")) { $clean += $newline }
    $entries = foreach ($domain in $Dominios) { "0.0.0.0 $domain"; ":: $domain" }
    return $clean + $script:InicioBloco + $newline + ($entries -join $newline) + $newline + $script:FimBloco + $newline
}

function Get-DominiosAtivos {
    param([string]$Texto)
    $block = Get-BlocoLocal $Texto
    if ($block.Matches.Count -eq 0) { return @() }
    return @([regex]::Matches($block.Matches[0].Groups[1].Value, '(?m)^0\.0\.0\.0\s+(\S+)\r?$') | ForEach-Object { $_.Groups[1].Value })
}

function Set-BloqueioLocal {
    param([string]$HostsPath, [string[]]$Dominios, [string]$BackupDirectory)
    # Read and write while holding an exclusive lock, preserving the original encoding.
    $stream = [IO.File]::Open($HostsPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $before = $null
    try {
        $memory = New-Object IO.MemoryStream
        $stream.CopyTo($memory)
        $before = $memory.ToArray()
        $memory.Dispose()
        $encoding = New-Object Text.UTF8Encoding($false, $true)
        $offset = 0
        if ($before.Length -ge 3 -and $before[0] -eq 239 -and $before[1] -eq 187 -and $before[2] -eq 191) {
            $encoding = New-Object Text.UTF8Encoding($true, $true); $offset = 3
        } elseif ($before.Length -ge 2 -and $before[0] -eq 255 -and $before[1] -eq 254) {
            $encoding = [Text.Encoding]::Unicode; $offset = 2
        } elseif ($before.Length -ge 2 -and $before[0] -eq 254 -and $before[1] -eq 255) {
            $encoding = [Text.Encoding]::BigEndianUnicode; $offset = 2
        }
        $original = $encoding.GetString($before, $offset, $before.Length - $offset)
        $updated = New-HostsText -Original $original -Dominios $Dominios
        if ($updated -ceq $original) { return }
        [IO.Directory]::CreateDirectory($BackupDirectory) | Out-Null
        $backup = Join-Path $BackupDirectory ('hosts-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N') + '.bak')
        [IO.File]::WriteAllBytes($backup, $before)
        [byte[]]$bytes = @($encoding.GetPreamble()) + @($encoding.GetBytes($updated))
        try {
            $stream.Position = 0
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.SetLength($bytes.Length)
            $stream.Flush($true)
        } catch {
            $stream.Position = 0
            $stream.Write($before, 0, $before.Length)
            $stream.SetLength($before.Length)
            $stream.Flush($true)
            throw
        }
    } finally { $stream.Dispose() }
}
