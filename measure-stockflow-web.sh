#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
echo "StockFlow: Domain + Application + Web, LINE COVERAGE"
if ! command -v dotnet >/dev/null; then
  echo "ERROR: dotnet SDK not found." >&2; exit 1
fi
# If tests fail, preserve their log and stop. Never claim successful coverage after failed tests.
dotnet test TradingManagementSystem.Tests/TradingManagementSystem.Tests.csproj \
  --collect:"XPlat Code Coverage" --settings coverage.web.runsettings \
  --logger "console;verbosity=minimal" --results-directory TestResults-Web
REPORT=$(find TestResults-Web -name 'coverage.cobertura.xml' -type f -printf '%T@ %p\n' | sort -nr | head -n 1 | cut -d' ' -f 2-)
if [ -z "$REPORT" ]; then echo "ERROR: No Cobertura report found" >&2; exit 1; fi
if ! command -v reportgenerator >/dev/null; then
  export PATH="$PATH:$HOME/.dotnet/tools"
fi
if command -v reportgenerator >/dev/null; then
  reportgenerator "-reports:$REPORT" "-targetdir:CoverageReport-Web" "-reporttypes:Html;TextSummary"
  echo
  echo "============ STOCKFLOW WEB COVERAGE ============"
  grep -E '^[[:space:]]*(Line coverage:|Covered lines:|Uncovered lines:|Coverable lines:|Total lines:|Assemblies:|Classes:)' CoverageReport-Web/Summary.txt || true
  echo "HTML: CoverageReport-Web/index.html"
else
  echo "Coverage XML: $REPORT"
  echo "Install HTML generator with: dotnet tool install -g dotnet-reportgenerator-globaltool"
fi
echo "Scope: Domain, Application, Web. Infrastructure/Demo are not in this percentage."
