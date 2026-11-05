using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingManagementSystem.Domain.Enums;
using MongoDB.Driver;
using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers.Api;

[ApiController]
[Route("api/sales")]
public class SalesApiController : ControllerBase
{
    private readonly MongoDbContext _context;
    private readonly SaleService _saleService;

    public SalesApiController(MongoDbContext context, SaleService saleService)
    {
        _context = context;
        _saleService = saleService;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Director,TradingPointManager,Seller")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        var salesFilter = actor.Role switch
        {
            EmployeeRole.Seller => Builders<Sale>.Filter.Eq(s => s.SellerId, actor.Id),
            EmployeeRole.TradingPointManager => Builders<Sale>.Filter.Eq(s => s.TradingPointId, actor.TradingPointId),
            _ => Builders<Sale>.Filter.Empty
        };
        var sales = await _context.Sales.Find(salesFilter).ToListAsync();
        return Ok(sales);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Director,TradingPointManager,Seller")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        var sale = await _context.Sales.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (sale == null) return NotFound();
        if (actor.Role == EmployeeRole.Seller && sale.SellerId != actor.Id) return NotFound();
        if (actor.Role == EmployeeRole.TradingPointManager && sale.TradingPointId != actor.TradingPointId) return NotFound();
        return Ok(sale);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Seller")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateSaleRequest request)
    {
        var actor = await CurrentAsync();
        if (actor == null || actor.Role != EmployeeRole.Seller) return Forbid();
        if (string.IsNullOrWhiteSpace(actor.TradingPointId) || request.TradingPointId != actor.TradingPointId)
            return Forbid();
        // A client-generated request ID helps prevent accidental repeated sales.
        if (!Guid.TryParse(request.RequestId, out _))
            return BadRequest("RequestId must be a valid GUID.");

        if (string.IsNullOrWhiteSpace(request.TradingPointId))
            return BadRequest("TradingPointId is required.");

        if (!Enum.IsDefined(request.PaymentMethod))
            return BadRequest("Invalid payment method. Use 0 (Cash) or 1 (Card).");

        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("At least one sale item is required.");

        if (await _context.Sales.Find(s => s.Id == request.RequestId).AnyAsync())
            return Conflict("This requestId has already been used for a sale.");

        if (!await _context.TradingPoints
            .Find(p => p.Id == request.TradingPointId).AnyAsync())
            return BadRequest("Trading point does not exist.");

        var inventory = await _context.InventoryItems
            .Find(i => i.TradingPointId == request.TradingPointId)
            .ToListAsync();

        var sale = new Sale
        {
            Id = request.RequestId,
            SellerId = actor.Id, // Never trust a caller-supplied seller ID.
            TradingPointId = request.TradingPointId,
            PaymentMethod = request.PaymentMethod
        };

        var uniqueProducts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductId) || item.Quantity <= 0)
                return BadRequest("Each item requires a productId and positive quantity.");

            if (!uniqueProducts.Add(item.ProductId))
                return BadRequest("Duplicate product IDs in the same sale are not allowed.");

            var product = await _context.Products
                .Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();

            if (product == null)
                return BadRequest($"Product {item.ProductId} does not exist.");

            sale.AddItem(new SaleItem
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.SellingPrice
            });
        }

        // Validate all items using the existing business-logic service.
        // This changes only the in-memory inventory snapshot, not MongoDB yet.
        try
        {
            _saleService.CompleteSale(sale, inventory);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        // A conditional MongoDB update prevents a single stock value from
        // dropping below zero under concurrent requests.
        var applied = new List<(string InventoryId, int Quantity)>();
        try
        {
            foreach (var item in sale.Items)
            {
                var stock = inventory.First(i => i.ProductId == item.ProductId);
                var filter = Builders<InventoryItem>.Filter.And(
                    Builders<InventoryItem>.Filter.Eq(i => i.Id, stock.Id),
                    Builders<InventoryItem>.Filter.Gte(i => i.Quantity, item.Quantity));
                var update = Builders<InventoryItem>.Update
                    .Inc(i => i.Quantity, -item.Quantity);

                var updated = await _context.InventoryItems
                    .FindOneAndUpdateAsync(filter, update);

                if (updated == null)
                    throw new InvalidOperationException(
                        "Not enough stock (it may have changed during checkout).");

                applied.Add((stock.Id, item.Quantity));
            }

            await _context.Sales.InsertOneAsync(sale);
        }
        catch (InvalidOperationException ex)
        {
            await RestoreStockAsync(applied);
            return Conflict(ex.Message);
        }
        catch
        {
            await RestoreStockAsync(applied);
            throw;
        }

        return CreatedAtAction(nameof(GetById), new { id = sale.Id }, sale);
    }

    private async Task<Employee?> CurrentAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _context.Employees.Find(e => e.Id == userId && e.IsActive).FirstOrDefaultAsync();
    }

    private async Task RestoreStockAsync(List<(string InventoryId, int Quantity)> applied)
    {
        // Best-effort compensation on failure, not a database transaction.
        foreach (var entry in applied)
        {
            await _context.InventoryItems.UpdateOneAsync(
                i => i.Id == entry.InventoryId,
                Builders<InventoryItem>.Update.Inc(i => i.Quantity, entry.Quantity));
        }
    }
}

public class CreateSaleRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string TradingPointId { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public List<CreateSaleItemRequest> Items { get; set; } = new();
}

public class CreateSaleItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

