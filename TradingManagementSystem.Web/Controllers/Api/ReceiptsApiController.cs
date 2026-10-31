
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers.Api;

[ApiController]
[Route("api/receipts")]
public class ReceiptsApiController : ControllerBase
{
    private readonly MongoDbContext _context;
    private readonly ReceiptService _receiptService;

    public ReceiptsApiController(
        MongoDbContext context,
        ReceiptService receiptService)
    {
        _context = context;
        _receiptService = receiptService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var receipts = await _context.Receipts
            .Find(_ => true)
            .ToListAsync();

        return Ok(receipts);
    }

    [HttpPost]
    public async Task<IActionResult> Register(
        RegisterReceiptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PurchaseId))
            return BadRequest("Purchase ID is required.");

        var purchase = await _context.Purchases
            .Find(p => p.Id == request.PurchaseId)
            .FirstOrDefaultAsync();

        if (purchase == null)
            return NotFound("Purchase not found.");

        if (purchase.Status == PurchaseStatus.Received ||
            purchase.Status == PurchaseStatus.Cancelled)
            return Conflict("Purchase cannot be received.");

        if (await _context.Receipts
            .Find(r => r.PurchaseId == purchase.Id)
            .AnyAsync())
            return Conflict("Receipt already exists.");

        if (purchase.Items.Count == 0 ||
            purchase.Items.Any(i => i.Quantity <= 0))
            return BadRequest("Invalid purchase items.");

        var inventoryItems = await _context.InventoryItems
            .Find(i => i.TradingPointId == purchase.TradingPointId)
            .ToListAsync();

        foreach (var productId in purchase.Items
            .Select(i => i.ProductId).Distinct())
        {
            if (inventoryItems.Any(i => i.ProductId == productId))
                continue;

            inventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = productId,
                TradingPointId = purchase.TradingPointId,
                MinimumQuantity = 5
            });
        }

        var receipt = new Receipt
        {
            Id = Guid.NewGuid().ToString(),
            PurchaseId = purchase.Id
        };

        try
        {
            _receiptService.RegisterReceipt(
                receipt, purchase, inventoryItems);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        foreach (var inventoryItem in inventoryItems.Where(
            i => purchase.Items.Any(
                p => p.ProductId == i.ProductId)))
        {
            await _context.InventoryItems.ReplaceOneAsync(
                i => i.Id == inventoryItem.Id,
                inventoryItem,
                new ReplaceOptions { IsUpsert = true });
        }

        await _context.Purchases.ReplaceOneAsync(
            p => p.Id == purchase.Id, purchase);

        await _context.Receipts.InsertOneAsync(receipt);

        return StatusCode(201, receipt);
    }
}

public class RegisterReceiptRequest
{
    public string PurchaseId { get; set; } = string.Empty;
}
