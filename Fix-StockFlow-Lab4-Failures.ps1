# Fix three failing Lab 4 tests in StockFlow. Does not change database or tests.
# Run from project root: powershell -NoProfile -ExecutionPolicy Bypass -File ".\Fix-StockFlow-Lab4-Failures.ps1"
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$inventoryPath = Join-Path $root 'TradingManagementSystem.Domain\Entities\InventoryItem.cs'
$salePath = Join-Path $root 'TradingManagementSystem.Application\Services\SaleService.cs'
foreach ($path in @($inventoryPath, $salePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Cannot find expected C# file: $path. Place this script next to TradingManagementSystem.slnx."
    }
}

$inventory = [System.IO.File]::ReadAllText($inventoryPath)
$sale = [System.IO.File]::ReadAllText($salePath)

# Validate BOTH source files before making any changes.
$oldStock = 'return Quantity >= quantity;'
$newStock = 'return quantity > 0 && Quantity >= quantity;'
if ($inventory.Contains($oldStock)) {
    if ($inventory.Split([string[]]@($oldStock), [System.StringSplitOptions]::None).Length -ne 2) {
        throw 'Unexpected InventoryItem.cs: more than one stock check. Nothing changed.'
    }
    $updatedInventory = $inventory.Replace($oldStock, $newStock)
} elseif ($inventory.Contains($newStock)) {
    $updatedInventory = $inventory
} else {
    throw 'Unrecognized InventoryItem.HasEnough implementation. Nothing changed; send InventoryItem.cs.'
}

$markerFirst = '        foreach (var saleItem in sale.Items)'
$markerEnd = '        sale.Complete();'
$start = $sale.IndexOf($markerFirst, [System.StringComparison]::Ordinal)
$end = if ($start -ge 0) { $sale.IndexOf($markerEnd, $start, [System.StringComparison]::Ordinal) } else { -1 }
$newAlgorithmMarker = 'var requested = sale.Items'
if ($start -ge 0 -and $end -gt $start) {
    $oldSection = $sale.Substring($start, $end - $start)
    $loopCount = [regex]::Matches($oldSection, 'foreach\s*\(var saleItem in sale.Items\)').Count
    $hasStockValidation = $oldSection.Contains('_inventoryService.HasEnoughStock(')
    $hasStockDecrease = $oldSection.Contains('_inventoryService.DecreaseStock(')
    if ($loopCount -ne 2 -or -not $hasStockValidation -or -not $hasStockDecrease) {
        throw 'Unrecognized SaleService validation/decrement code. Nothing changed; send SaleService.cs.'
    }
    $nl = if ($sale.Contains("`r`n")) { "`r`n" } else { "`n" }
    $replacement = @'
        // Validate the entire sale before changing any inventory quantities.
        if (sale.Items.Count == 0)
        {
            throw new InvalidOperationException("Sale must contain at least one item.");
        }

        foreach (var saleItem in sale.Items)
        {
            if (saleItem.Quantity <= 0)
            {
                throw new InvalidOperationException("Sale item quantity must be greater than zero.");
            }
        }

        // A product can appear on several lines: check its COMBINED demand.
        var requested = sale.Items
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => (long)item.Quantity)
            })
            .ToList();

        foreach (var request in requested)
        {
            var inventoryItem = inventoryItems.FirstOrDefault(
                i => i.ProductId == request.ProductId &&
                     i.TradingPointId == sale.TradingPointId);

            if (inventoryItem == null)
            {
                throw new InvalidOperationException("Inventory item was not found.");
            }

            if (request.Quantity > int.MaxValue ||
                !_inventoryService.HasEnoughStock(inventoryItem, (int)request.Quantity))
            {
                throw new InvalidOperationException("Not enough products in stock.");
            }
        }

        // Only after every product has been validated do we change stock.
        foreach (var request in requested)
        {
            var inventoryItem = inventoryItems.First(
                i => i.ProductId == request.ProductId &&
                     i.TradingPointId == sale.TradingPointId);

            _inventoryService.DecreaseStock(inventoryItem, (int)request.Quantity);
        }

'@
    $replacement = $replacement.Replace("`r`n", "`n").Replace("`n", $nl)
    # PowerShell here-strings do not include a trailing newline before the closing marker.
    $updatedSale = $sale.Substring(0, $start) + $replacement + $nl + $sale.Substring($end)
    if (-not $updatedSale.Contains('using System.Linq;')) {
        $prefix = 'using System.Linq;' + $nl
        # Explicit LINQ import for GroupBy / Select / ToList.
        $updatedSale = $prefix + $updatedSale
    }
} elseif ($sale.Contains($newAlgorithmMarker)) {
    Write-Host 'SaleService already appears to contain grouped validation. Skipping its update.' -ForegroundColor Yellow
    $updatedSale = $sale
} else {
    throw 'Unrecognized SaleService.cs; nothing changed. Send this file so it can be patched safely.'
}

$backup = Join-Path ([System.IO.Path]::GetTempPath()) ('StockFlow-Lab4-Backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
Copy-Item -LiteralPath $inventoryPath -Destination (Join-Path $backup 'InventoryItem.cs')
Copy-Item -LiteralPath $salePath -Destination (Join-Path $backup 'SaleService.cs')

$utf8 = New-Object System.Text.UTF8Encoding($false)
try {
    if ($updatedInventory -cne $inventory) { [System.IO.File]::WriteAllText($inventoryPath, $updatedInventory, $utf8) }
    if ($updatedSale -cne $sale) { [System.IO.File]::WriteAllText($salePath, $updatedSale, $utf8) }
} catch {
    Copy-Item -LiteralPath (Join-Path $backup 'InventoryItem.cs') -Destination $inventoryPath -Force
    Copy-Item -LiteralPath (Join-Path $backup 'SaleService.cs') -Destination $salePath -Force
    throw
}
Write-Host 'DONE. Updated InventoryItem.HasEnough and SaleService validation; tests and MongoDB unchanged.' -ForegroundColor Green
Write-Host ('Backup: ' + $backup)
Write-Host 'NEXT: dotnet test .\TradingManagementSystem.Tests\TradingManagementSystem.Tests.csproj'
Write-Host 'Then: powershell -NoProfile -ExecutionPolicy Bypass -File ".\Measure-StockFlow-Coverage.ps1"'
