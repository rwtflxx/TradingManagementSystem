# StockFlow Lab 5 - repository, strategy, observer
# Run from Git Bash: powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./Install-StockFlow-Lab5.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (!(Test-Path (Join-Path $root 'TradingManagementSystem.slnx'))) {
    throw 'Place this .ps1 next to TradingManagementSystem.slnx (the repository root).'
}

$payloadBase64 = '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'
$compressed = [Convert]::FromBase64String($payloadBase64)
$memory = [System.IO.MemoryStream]::new([byte[]]$compressed)
$zipReader = [System.IO.Compression.GZipStream]::new($memory, [System.IO.Compression.CompressionMode]::Decompress)
$reader = [System.IO.StreamReader]::new($zipReader, [Text.Encoding]::UTF8)
try { $config = ConvertFrom-Json ($reader.ReadToEnd()) }
finally { $reader.Dispose(); $zipReader.Dispose(); $memory.Dispose() }

function Count-Exact([string] $content, [string] $needle) {
    $pos = 0; $count = 0
    while (($pos = $content.IndexOf($needle, $pos, [StringComparison]::Ordinal)) -ge 0) {
        $count++; $pos += $needle.Length
    }
    return $count
}

# IMPORTANT: prepare ALL changed files first; do not touch the source if preflight fails.
$prepared = @{}
$patchedPaths = @()
foreach ($patch in $config.patches) {
    $rel = [string]$patch.path
    $path = Join-Path $root ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
    if (!(Test-Path $path)) { throw "Missing expected project file: $rel. Nothing was changed." }
    $value = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    foreach ($edit in $patch.edits) {
        $old = [string]$edit.old; $new = [string]$edit.new
        if ((Count-Exact $value $old) -ne [int]$edit.expectedCount) {
            throw "Unrecognized version of $rel (expected one exact code block). Nothing was changed. Send the current ZIP for a compatible patch."
        }
        $value = $value.Replace($old, $new)
    }
    $prepared[$rel] = $value
    $patchedPaths += $rel
}
foreach ($entry in $config.newFiles) {
    $rel = [string]$entry.path
    $path = Join-Path $root ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
    if (Test-Path $path) { throw "Already exists: $rel. Nothing was changed; don't run this installer twice." }
    $prepared[$rel] = [string]$entry.content
}

$backupRoot = Join-Path $env:TEMP ('StockFlow-Lab5-Backup-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
foreach ($rel in $patchedPaths) {
    $src = Join-Path $root ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
    $dst = Join-Path $backupRoot ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
    New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
    Copy-Item -LiteralPath $src -Destination $dst
}

$written = New-Object System.Collections.Generic.List[string]
try {
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    foreach ($rel in $prepared.Keys) {
        $dst = Join-Path $root ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
        New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
        $written.Add($rel)
        [IO.File]::WriteAllText($dst, $prepared[$rel], $utf8)
    }
} catch {
    foreach ($rel in $written) {
        $dst = Join-Path $root ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if ($patchedPaths -contains $rel) {
            $src = Join-Path $backupRoot ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
            Copy-Item -LiteralPath $src -Destination $dst -Force
        } elseif (Test-Path $dst) { Remove-Item -LiteralPath $dst -Force }
    }
    throw
}
Write-Host ''
Write-Host 'SUCCESS: StockFlow Lab 5 patterns installed.' -ForegroundColor Green
Write-Host 'Repository: products via IProductRepository / MongoProductRepository.'
Write-Host 'Strategy: standard + seasonal target stock calculations.'
Write-Host 'Observer: in-memory inventory events with explicit subscriptions.'
Write-Host "Original changed files backed up under: $backupRoot"
Write-Host 'NEXT (Git Bash): dotnet test TradingManagementSystem.Tests/TradingManagementSystem.Tests.csproj'
Write-Host 'NEXT (Git Bash): bash ./measure-stockflow-web.sh'
Write-Host 'Do not overwrite files with the older Lab 5 prototype after installing this patch.'
