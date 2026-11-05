# StockFlow Lab 4: repair Web integration tests' isolated MongoDB connection.
# Run in the solution root. This modifies ONLY the Web integration test source file.
$ErrorActionPreference = 'Stop'

$root = (Get-Location).Path
$solution = Join-Path $root 'TradingManagementSystem.slnx'
$tests = Join-Path $root 'TradingManagementSystem.Tests\StockFlowWebTests.cs'
if (!(Test-Path -LiteralPath $solution) -or !(Test-Path -LiteralPath $tests)) {
    throw 'Open Git Bash in the directory with TradingManagementSystem.slnx and TradingManagementSystem.Tests.'
}

$original = [System.IO.File]::ReadAllText($tests)
$registration = 'services.RemoveAll<TradingManagementSystem.Infrastructure.Data.MongoDbContext>();'
if ($original.Contains($registration)) {
    Write-Host 'The isolated MongoDB registration is already installed; no changes made.' -ForegroundColor Yellow
    Write-Host 'Next in Git Bash: bash ./measure-stockflow-web.sh' -ForegroundColor Cyan
    exit 0
}

$import = 'using Microsoft.Extensions.DependencyInjection;'
if (!$original.Contains($import) -or !$original.Contains('builder.ConfigureTestServices(services =>') -or
    !$original.Contains('public string TemporaryDatabase { get; }') -or
    !$original.Contains('private readonly string _connectionString =')) {
    throw 'This is not the expected StockFlow Web test harness; no files were changed.'
}

$pattern = '(?m)^([ \t]*builder\.ConfigureTestServices\(services =>[ \t]*\r?\n[ \t]*\{[ \t]*\r?\n)'
$matches = [regex]::Matches($original, $pattern)
if ($matches.Count -ne 1) {
    throw ('Expected one ConfigureTestServices block; found ' + $matches.Count + '. Nothing changed.')
}

$eol = if ($original.Contains("`r`n")) { "`r`n" } else { "`n" }
$updated = $original
if (!$updated.Contains('using Microsoft.Extensions.DependencyInjection.Extensions;')) {
    $updated = $updated.Replace($import, $import + $eol + 'using Microsoft.Extensions.DependencyInjection.Extensions;')
}

$add = @(
    '            // The Web app creates its MongoDbContext before test configuration overrides are applied.',
    '            // Replace that registration to isolate all HTTP tests from the real StockFlow database.',
    '            services.RemoveAll<TradingManagementSystem.Infrastructure.Data.MongoDbContext>();',
    '            services.AddSingleton(new TradingManagementSystem.Infrastructure.Data.MongoDbContext(_connectionString, TemporaryDatabase));'
) -join $eol
$replacement = '$1' + $add + $eol
$updated = [regex]::Replace($updated, $pattern, $replacement)
if (!$updated.Contains($registration) -or !$updated.Contains('services.AddSingleton(new TradingManagementSystem.Infrastructure.Data.MongoDbContext(_connectionString, TemporaryDatabase));')) {
    throw 'Verification of the proposed test-only change failed; no files were changed.'
}

$backup = Join-Path ([System.IO.Path]::GetTempPath()) ('StockFlow-WebTests-Backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.cs')
Copy-Item -LiteralPath $tests -Destination $backup -ErrorAction Stop
[System.IO.File]::WriteAllText($tests, $updated, (New-Object System.Text.UTF8Encoding($false)))

Write-Host 'SUCCESS: Web integration tests now use the isolated temporary MongoDB database.' -ForegroundColor Green
Write-Host ('Backup of the previous test file: ' + $backup)
Write-Host 'Production code, normal MongoDB records and users were not changed.'
Write-Host 'Next in Git Bash: bash ./measure-stockflow-web.sh' -ForegroundColor Cyan
