#requires -Version 5.1
# StockFlow Lab 4 - tests only. No application code is modified.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root 'TradingManagementSystem.slnx'
if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
    throw 'Put this .ps1 beside TradingManagementSystem.slnx in the repository root.'
}
foreach ($rel in @(
    'TradingManagementSystem.Domain/TradingManagementSystem.Domain.csproj',
    'TradingManagementSystem.Application/TradingManagementSystem.Application.csproj',
    'TradingManagementSystem.Application/Services/SmartInventoryService.cs'
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $rel) -PathType Leaf)) {
        throw ('Required source missing: ' + $rel)
    }
}
function FileHash([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return '' }
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}
# Embedded payload includes tests and coverage config; two source patches from the earlier installer are NOT used.
$b64 = @'
UEsDBBQAAAAIAG9+ZV2K6eOqQQEAAEoDAABCAAAAVHJhZGluZ01hbmFnZW1lbnRTeXN0ZW0uVGVzdHMvVHJhZGluZ01hbmFnZW1lbnRTeXN0ZW0u
VGVzdHMuY3Nwcm9qnVLLbsIwELz3K6J8wIYU1KpSiFSpD3EAoZL21IvrbJCLY0drm5a/r5MAMg8J1Ovs7OzO7GZz0t/IbbQoV+N4KjhpoysLs+cC
PBTnN1GUeU6DZDevpF3TIh4rGC3RvhCr8UfTKldo0wEMsuS40NMnZs74in1JzCsmDWZJgOwoBRq73Se35DpSiG15dSMFF/bdCLU0OapWw1MP4Z47
c1J2I3asPdD6Sk6MZROLdWiz23GJb1ghoeIYTRSXrsTjrNo1u8CiDyQjtBrH6T2kI0jjKLmo9euUsEHrLTzA8OpGIKcUEqyFcT5b60qhA7EhpDC6
RozrNZJEC1xL6fPWFKjcwWCv4sMOYzoNrb/XmREAnwWx0l9oypTfokZlFxvj++FJ10yoC2XgpvHigZv/jHps2ldh1hu7hnM49MB890L9b/4BUEsD
BBQAAAAIAG9+ZV28nQPtYQYAAG0iAAAsAAAAVHJhZGluZ01hbmFnZW1lbnRTeXN0ZW0uVGVzdHMvRG9tYWluVGVzdHMuY3PNWW1v2zYQ/u5foemT
DCRC5CDrh6zFXMdZgyKJFzvbsGIIGImxuVKSS1JOjCL/fUeJlCVblOk47RogsUUd7+W543Mkk3GSTJ0JQxF8XqIETXGMEzFecoFj/yyNEUn8YSKI
IJifdjI78SwuZf/KEiJOO50ExZjPUYiNsyeYC5jWmWf3lIROSBHnzoilURaK/F3na8eBn0/nKBT/5F+V6CIlkXM7j5DAZ0iguzFaYP4HoiSCPxnm
XjeXLqbLnwVizrzQ7Lx1Evyo7Xjd01JICfgrxZ77ES/vU8Qi98Bx+2GIOU8ZAAOPxydHMQzejt+7FR19zjET/vBLhmhttlZ+BbAY5esW9JQBeDNN
2dI0rfBES48xpYD2iJHQbEh6vZpyhnnIyFyQNFEznjsF8pMZBsMF9p8uElCscHG7DYOOo4cNebrB/+JQ8GE8F0sJhMcFkzUja8U6Zc5XR86FQfea
kSlJEHWd541IJzOWPvJf+myayZobPoU4j/Cd53Wdt++asi3dkHkGaAL4dLtG/ErDjYnV6G2tW4XHFZ4iQRY4z5n3AwDhXuFHVyFxGMTfBYs+peDm
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
bS5UZXN0cy9Eb21haW5UZXN0cy5jc1BLAQIUAxQAAAAIAG9+ZV0i3R9KWwUAADUjAAAxAAAAAAAAAAAAAACAAUwIAABUcmFkaW5nTWFuYWdlbWVu
dFN5c3RlbS5UZXN0cy9BcHBsaWNhdGlvblRlc3RzLmNzUEsBAhQDFAAAAAgAb35lXUyJeI0LBwAA9iYAADQAAAAAAAAAAAAAAIAB9g0AAFRyYWRp
bmdNYW5hZ2VtZW50U3lzdGVtLlRlc3RzL1NtYXJ0SW52ZW50b3J5VGVzdHMuY3NQSwECFAMUAAAACABvfmVdE7mYBiABAACNAgAAFAAAAAAAAAAA
AAAAgAFTFQAAY292ZXJhZ2UucnVuc2V0dGluZ3NQSwECFAMUAAAACABvfmVdGpXMzX0DAACtBwAAHgAAAAAAAAAAAAAAgAGlFgAATWVhc3VyZS1T
dG9ja0Zsb3ctQ292ZXJhZ2UucHMxUEsBAhQDFAAAAAgAb35lXdiQ8+exAwAApAYAABUAAAAAAAAAAAAAAIABXhoAAExBQjRfSW5zdHJ1Y3Rpb25z
LnR4dFBLAQIUAxQAAAAIAG9+ZV0r7uGucgEAAGwEAAA4AAAAAAAAAAAAAACAAUIeAABUcmFkaW5nTWFuYWdlbWVudFN5c3RlbS5Eb21haW4vRW50
aXRpZXMvSW52ZW50b3J5SXRlbS5jc1BLAQIUAxQAAAAIAG9+ZV3zmj1wAQMAAMcJAAA7AAAAAAAAAAAAAACAAQogAABUcmFkaW5nTWFuYWdlbWVu
dFN5c3RlbS5BcHBsaWNhdGlvbi9TZXJ2aWNlcy9TYWxlU2VydmljZS5jc1BLBQYAAAAACQAJACsDAABkIwAAAAA=
'@
$bytes = [Convert]::FromBase64String(($b64 -replace '\s', ''))
$sha = [System.Security.Cryptography.SHA256]::Create()
$actual = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant()
if ($actual -ne 'dad9842387b83a5d3aa0d176b19992714b3f201c16a0fdcfdbf43166f0767da7') {
    throw 'Payload checksum mismatch; no files changed.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$work = Join-Path ([IO.Path]::GetTempPath()) ('StockFlowLab4Tests_' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
try {
    $zipFile = Join-Path $work 'payload.zip'
    [IO.File]::WriteAllBytes($zipFile, $bytes)
    $payloadDir = Join-Path $work 'payload'
    [IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $payloadDir)
    $files = @(
        'TradingManagementSystem.Tests/TradingManagementSystem.Tests.csproj',
        'TradingManagementSystem.Tests/DomainTests.cs',
        'TradingManagementSystem.Tests/ApplicationTests.cs',
        'TradingManagementSystem.Tests/SmartInventoryTests.cs',
        'coverage.runsettings',
        'Measure-StockFlow-Coverage.ps1',
        'LAB4_Instructions.txt'
    )
    foreach ($rel in $files) {
        $src = Join-Path $payloadDir ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $dst = Join-Path $root ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $src -PathType Leaf)) { throw ('Payload missing ' + $rel) }
        if ((Test-Path -LiteralPath $dst -PathType Leaf) -and (FileHash $src) -ne (FileHash $dst)) {
            throw ('Existing local test/config file differs: ' + $rel + '. No changes made.')
        }
    }
    $solutionText = [IO.File]::ReadAllText($solution)
    if (-not $solutionText.Contains('</Solution>')) { throw 'Unsupported solution format.' }
    $needsProject = -not $solutionText.Contains('TradingManagementSystem.Tests/TradingManagementSystem.Tests.csproj')
    $backup = Join-Path ([IO.Path]::GetTempPath()) ('StockFlow-Lab4-Solution-Backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $solution -Destination (Join-Path $backup 'TradingManagementSystem.slnx') -Force
    foreach ($rel in $files) {
        $src = Join-Path $payloadDir ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $dst = Join-Path $root ($rel.Replace('/', [IO.Path]::DirectorySeparatorChar))
        New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force | Out-Null
        Copy-Item -LiteralPath $src -Destination $dst -Force
    }
    if ($needsProject) {
        $nl = if ($solutionText.Contains("`r`n")) { "`r`n" } else { "`n" }
        $insert = '  <Project Path="TradingManagementSystem.Tests/TradingManagementSystem.Tests.csproj" />' + $nl
        [IO.File]::WriteAllText($solution, $solutionText.Replace('</Solution>', $insert + '</Solution>'),
            (New-Object System.Text.UTF8Encoding($false)))
    }
    Write-Host 'SUCCESS: StockFlow Lab 4 tests installed without changing the app code.' -ForegroundColor Green
    Write-Host ('Solution backup: ' + $backup)
    Write-Host 'Run: powershell -NoProfile -ExecutionPolicy Bypass -File ".\Measure-StockFlow-Coverage.ps1"' -ForegroundColor Cyan
    Write-Host 'If tests fail, share the error. We will fix the actual business rule; do not delete failing tests.' -ForegroundColor Yellow
}
finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
}
