# Run all StockFlow unit tests and print actual Cobertura coverage percentages.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'TradingManagementSystem.Tests/TradingManagementSystem.Tests.csproj'
$settings = Join-Path $root 'coverage.runsettings'
if (-not (Test-Path -LiteralPath $project)) { throw 'Test project not found. Run Install-StockFlow-Lab4-Complete.ps1 first.' }
if (-not (Test-Path -LiteralPath $settings)) { throw 'coverage.runsettings not found.' }
Write-Host 'Running xUnit tests and collecting Domain + Application coverage...' -ForegroundColor Cyan
& dotnet test $project '--collect:XPlat Code Coverage' --settings $settings --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { throw ('dotnet test exited with code ' + $LASTEXITCODE + '. Fix failures before reporting coverage.') }
$testDir = Join-Path $root 'TradingManagementSystem.Tests/TestResults'
$report = Get-ChildItem -LiteralPath $testDir -Recurse -Filter 'coverage.cobertura.xml' -File |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $report) { throw 'Tests completed, but coverage.cobertura.xml was not generated.' }
[xml]$data = Get-Content -LiteralPath $report.FullName -Raw
$line = [double]::Parse([string]$data.coverage.'line-rate', [Globalization.CultureInfo]::InvariantCulture) * 100
$branch = [double]::Parse([string]$data.coverage.'branch-rate', [Globalization.CultureInfo]::InvariantCulture) * 100
Write-Host ''
Write-Host ('LINE COVERAGE (Domain + Application): {0:N2}%' -f $line) -ForegroundColor Green
Write-Host ('BRANCH COVERAGE: {0:N2}%' -f $branch) -ForegroundColor Cyan
Write-Host ('Report: ' + $report.FullName)
if ($line -lt 90) { Write-Host 'Coverage is below 90%. Send this output and we will cover the missing branches.' -ForegroundColor Yellow }
Write-Host 'Note: this percentage does not include the ASP.NET Core Web project.' -ForegroundColor Yellow
