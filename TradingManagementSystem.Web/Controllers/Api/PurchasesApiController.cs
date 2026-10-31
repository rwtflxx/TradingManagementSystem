
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers.Api;

[ApiController]
[Route("api/purchases")]
public class PurchasesApiController : ControllerBase
{
    private readonly MongoDbContext _context;

    public PurchasesApiController(MongoDbContext context)
    {
        _context = context;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Director,SupplyManager")]
    [HttpGet]
    public async Task<ActionResult<List<Purchase>>> GetAll()
    {
        var purchases = await _context.Purchases
            .Find(_ => true)
            .ToListAsync();

        return Ok(purchases);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Director,SupplyManager")]
    [HttpGet("{id}")]
    public async Task<ActionResult<Purchase>> GetById(string id)
    {
        var purchase = await _context.Purchases
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        if (purchase == null)
            return NotFound();

        return Ok(purchase);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Director,SupplyManager")]
    [HttpPost]
    public async Task<ActionResult<Purchase>> Create(
        CreatePurchaseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierId) ||
            string.IsNullOrWhiteSpace(request.TradingPointId))
            return BadRequest(
                "Supplier and trading point are required.");

        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("Add at least one product.");

        var supplierExists = await _context.Suppliers
            .Find(s => s.Id == request.SupplierId)
            .AnyAsync();

        if (!supplierExists)
            return BadRequest("Supplier does not exist.");

        var pointExists = await _context.TradingPoints
            .Find(p => p.Id == request.TradingPointId)
            .AnyAsync();

        if (!pointExists)
            return BadRequest("Trading point does not exist.");

        var purchase = new Purchase
        {
            Id = Guid.NewGuid().ToString(),
            SupplierId = request.SupplierId,
            TradingPointId = request.TradingPointId
        };

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductId) ||
                item.Quantity <= 0 ||
                item.PurchasePrice < 0)
                return BadRequest("Invalid purchase item.");

            if (purchase.Items.Any(i => i.ProductId == item.ProductId))
                return BadRequest(
                    "Duplicate products are not allowed.");

            var productExists = await _context.Products
                .Find(p => p.Id == item.ProductId)
                .AnyAsync();

            if (!productExists)
                return BadRequest("Product does not exist.");

            purchase.AddItem(new PurchaseItem
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                PurchasePrice = item.PurchasePrice
            });
        }

        await _context.Purchases.InsertOneAsync(purchase);

        return CreatedAtAction(
            nameof(GetById),
            new { id = purchase.Id },
            purchase);
    }
}

public class CreatePurchaseRequest
{
    public string SupplierId { get; set; } = string.Empty;

    public string TradingPointId { get; set; } = string.Empty;

    public List<CreatePurchaseItemRequest> Items { get; set; } = new();
}

public class CreatePurchaseItemRequest
{
    public string ProductId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal PurchasePrice { get; set; }
}

