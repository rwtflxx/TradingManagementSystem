# StockFlow Lab 4: repair test-only integration assertions and seeded password hash.
# No production controllers, domain code, or non-test MongoDB data are modified.
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$tests = Join-Path $root 'TradingManagementSystem.Tests'
$solution = Join-Path $root 'TradingManagementSystem.slnx'
if (!(Test-Path -LiteralPath $solution)) {
    throw 'Run this installer in the TradingManagementSystem solution root.'
}

$webTestPath = Join-Path $tests 'StockFlowWebTests.cs'
$additionalPath = Join-Path $tests 'StockFlowWebAdditionalCoverageTests.cs'
$smartPath = Join-Path $tests 'SmartInventoryTests.cs'
foreach ($file in @($webTestPath, $additionalPath, $smartPath)) {
    if (!(Test-Path -LiteralPath $file)) { throw ('Required test file not found: ' + $file) }
}

$utf8 = [System.Text.UTF8Encoding]::new($false)
$web = [System.IO.File]::ReadAllText($webTestPath)
$additional = [System.IO.File]::ReadAllText($additionalPath)
$smart = [System.IO.File]::ReadAllText($smartPath)

function Replace-Checked([string]$text, [string]$original, [string]$replacement, [int]$count, [string]$label) {
    $actual = [regex]::Matches($text, [regex]::Escape($original)).Count
    if ($actual -eq 0 -and $text.Contains($replacement)) { return $text } # already repaired
    if ($actual -ne $count) {
        throw ('Unexpected test version for ' + $label + ': expected ' + $count + ' occurrences, found ' + $actual + '. Nothing changed.')
    }
    return $text.Replace($original, $replacement)
}

# Razor HTML-encodes Ukrainian Cyrillic characters as entities; the user-facing
# text is correct, so decode the response body before checking message text.
$additionalFixed = Replace-Checked $additional `
    ', await response.Content.ReadAsStringAsync());' `
    ', WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));' `
    7 'HTML message assertions'

# Test fixture used a fake string in PasswordHash; Identity PasswordHasher expects
# a real base64-encoded password hash. Generate one only in the test seed.
$webFixed = Replace-Checked $web `
    'employee.ChangePassword("test-only-nonreal-hash");' `
    'employee.ChangePassword(new Microsoft.AspNetCore.Identity.PasswordHasher<Employee>().HashPassword(employee, "StockFlowTestPassword123!"));' `
    1 'test-only password hash'

# Address xUnit2031 analyzer warnings, preserving the original assertions.
$smartFixed = Replace-Checked $smart `
    'Assert.Single(results.Where(x => x.TradingPointId == "destination"))' `
    'Assert.Single(results, x => x.TradingPointId == "destination")' `
    5 'xUnit2031 warnings'

$changes = @()
if ($additionalFixed -cne $additional) { $changes += @{ Path = $additionalPath; Text = $additionalFixed } }
if ($webFixed -cne $web) { $changes += @{ Path = $webTestPath; Text = $webFixed } }
if ($smartFixed -cne $smart) { $changes += @{ Path = $smartPath; Text = $smartFixed } }
if ($changes.Count -eq 0) {
    Write-Host 'Tests already updated. Nothing changed.' -ForegroundColor Yellow
    exit 0
}

# Keep backups outside the Git repository.
$backup = Join-Path ([System.IO.Path]::GetTempPath()) ('StockFlow-Lab4-TestFix-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
[System.IO.Directory]::CreateDirectory($backup) | Out-Null
foreach ($change in $changes) {
    Copy-Item -LiteralPath $change.Path -Destination (Join-Path $backup ([System.IO.Path]::GetFileName($change.Path))) -ErrorAction Stop
}
try {
    foreach ($change in $changes) {
        [System.IO.File]::WriteAllText($change.Path, $change.Text, $utf8)
    }
} catch {
    foreach ($change in $changes) {
        $saved = Join-Path $backup ([System.IO.Path]::GetFileName($change.Path))
        Copy-Item -LiteralPath $saved -Destination $change.Path -Force
    }
    throw
}
Write-Host ('SUCCESS: Updated ' + $changes.Count + ' test files, production code untouched.') -ForegroundColor Green
Write-Host ('Backups: ' + $backup)
Write-Host 'Next: bash ./measure-stockflow-web.sh' -ForegroundColor Cyan
