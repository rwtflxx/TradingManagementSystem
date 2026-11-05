# StockFlow - safe additive Web integration tests installation
# Installs tests into existing solution without changing production C# logic or MongoDB data.
$ErrorActionPreference = 'Stop'

$solution = Join-Path (Get-Location) 'TradingManagementSystem.slnx'
$tests = Join-Path (Get-Location) 'TradingManagementSystem.Tests'
$projectFile = Join-Path $tests 'TradingManagementSystem.Tests.csproj'
$webProjectFile = Join-Path (Get-Location) 'TradingManagementSystem.Web/TradingManagementSystem.Web.csproj'
if (!(Test-Path -LiteralPath $solution) -or !(Test-Path -LiteralPath $projectFile) -or !(Test-Path -LiteralPath $webProjectFile)) {
    throw 'Open Git Bash or PowerShell in the root folder containing TradingManagementSystem.slnx and TradingManagementSystem.Tests.'
}

# Verify project format before any write.
$oldProject = [System.IO.File]::ReadAllText($projectFile)
if ($oldProject -notmatch '<TargetFramework>net10\.0</TargetFramework>' -or
    $oldProject -notmatch 'xunit' -or
    $oldProject -notmatch 'TradingManagementSystem.Application\\TradingManagementSystem.Application.csproj') {
    throw 'Unexpected test project layout. No files changed; send the current ZIP to review.'
}
$marker = '<PackageReference Include="coverlet.collector" Version="6.0.4" />'
if ($oldProject -notmatch 'Microsoft.AspNetCore.Mvc.Testing' -and !$oldProject.Contains($marker)) {
    throw 'Unexpected coverlet reference; refusing to alter tests project.'
}

$backupDir = Join-Path ([System.IO.Path]::GetTempPath()) ('StockFlow-WebTests-Backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
[System.IO.Directory]::CreateDirectory($backupDir) | Out-Null
Copy-Item -LiteralPath $projectFile -Destination (Join-Path $backupDir 'TradingManagementSystem.Tests.csproj')
$gitIgnore = Join-Path (Get-Location) '.gitignore'
if (Test-Path -LiteralPath $gitIgnore) { Copy-Item -LiteralPath $gitIgnore -Destination (Join-Path $backupDir '.gitignore') }

$base64 = @'
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
'@
$payloadZip = Join-Path $env:TEMP ('StockFlow-Web-Payload-' + [Guid]::NewGuid().ToString('N') + '.zip')
$unpack = Join-Path $env:TEMP ('StockFlow-Web-Unpack-' + [Guid]::NewGuid().ToString('N'))
try {
    [System.IO.File]::WriteAllBytes($payloadZip, [Convert]::FromBase64String($base64.Trim()))
    Expand-Archive -LiteralPath $payloadZip -DestinationPath $unpack -Force
    $destinations = @{
        'StockFlowWebTests.cs' = (Join-Path $tests 'StockFlowWebTests.cs');
        'coverage.web.runsettings' = (Join-Path (Get-Location) 'coverage.web.runsettings');
        'measure-stockflow-web.sh' = (Join-Path (Get-Location) 'measure-stockflow-web.sh');
    }
    foreach ($item in $destinations.GetEnumerator()) {
        if (Test-Path -LiteralPath $item.Value) {
            $newHash = (Get-FileHash -LiteralPath (Join-Path $unpack $item.Key) -Algorithm SHA256).Hash
            $oldHash = (Get-FileHash -LiteralPath $item.Value -Algorithm SHA256).Hash
            if ($newHash -ne $oldHash) { throw ('Refusing to replace existing file: ' + $item.Value) }
        }
    }

    $newProject = $oldProject
    if (!$newProject.Contains('Microsoft.AspNetCore.Mvc.Testing')) {
        $newProject = $newProject.Replace($marker,
            $marker + [Environment]::NewLine + '    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />')
    }
    if (!$newProject.Contains('TradingManagementSystem.Web.csproj')) {
        $reference = '    <ProjectReference Include="..\TradingManagementSystem.Web\TradingManagementSystem.Web.csproj" />'
        $projectMarker = '    <ProjectReference Include="..\TradingManagementSystem.Application\TradingManagementSystem.Application.csproj" />'
        if (!$newProject.Contains($projectMarker)) { throw 'Missing Application project reference; refusing write.' }
        $newProject = $newProject.Replace($projectMarker,$projectMarker + [Environment]::NewLine + $reference)
    }
    [System.IO.File]::WriteAllText($projectFile, $newProject, (New-Object System.Text.UTF8Encoding($false)))
    foreach ($item in $destinations.GetEnumerator()) {
        if (!(Test-Path -LiteralPath $item.Value)) {
            Copy-Item -LiteralPath (Join-Path $unpack $item.Key) -Destination $item.Value
        }
    }
    if (Test-Path -LiteralPath $gitIgnore) {
        $ignore = [System.IO.File]::ReadAllText($gitIgnore)
        $addition = @('/CoverageReport-Web/', '/TestResults-Web/')
        foreach ($line in $addition) {
            if (!$ignore.Contains($line)) { $ignore += [Environment]::NewLine + $line }
        }
        [System.IO.File]::WriteAllText($gitIgnore, $ignore + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
    }
    Write-Host 'SUCCESS: Web integration tests installed without replacing production code or MongoDB data.' -ForegroundColor Green
    Write-Host ('Backup: ' + $backupDir)
    Write-Host 'Git Bash next: bash ./measure-stockflow-web.sh' -ForegroundColor Cyan
    Write-Host 'This uses a separate temporary MongoDB database (local MongoDB must be running).'
}
finally {
    if (Test-Path -LiteralPath $payloadZip) { Remove-Item -LiteralPath $payloadZip -Force }
    if (Test-Path -LiteralPath $unpack) { Remove-Item -LiteralPath $unpack -Recurse -Force }
}
