using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers.Api;

[ApiController]
[Route("api/inventory")]
public class InventoryApiController : ControllerBase
{
    private readonly MongoDbContext _context;

    public InventoryApiController(MongoDbContext context)
    {
        _context = context;
    }

    // GET /api/inventory or GET /api/inventory?tradingPointId=...
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tradingPointId)
    {
        var filter = string.IsNullOrWhiteSpace(tradingPointId)
            ? Builders<InventoryItem>.Filter.Empty
            : Builders<InventoryItem>.Filter.Eq(i => i.TradingPointId, tradingPointId);
        var items = await _context.InventoryItems.Find(filter).ToListAsync();
        return Ok(items);
    }

    // Useful later for Smart Inventory.
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock([FromQuery] string? tradingPointId)
    {
        var filter = string.IsNullOrWhiteSpace(tradingPointId)
            ? Builders<InventoryItem>.Filter.Empty
            : Builders<InventoryItem>.Filter.Eq(i => i.TradingPointId, tradingPointId);
        var items = await _context.InventoryItems.Find(filter).ToListAsync();
        return Ok(items.Where(i => i.Quantity <= i.MinimumQuantity).ToList());
    }
}
