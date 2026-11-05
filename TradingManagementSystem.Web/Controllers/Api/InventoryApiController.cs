using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TradingManagementSystem.Domain.Enums;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers.Api;

[ApiController]
[Authorize(Roles = "Director,TradingPointManager,SupplyManager,Seller")]
[Route("api/inventory")]
public class InventoryApiController : ControllerBase
{
    private readonly MongoDbContext _context;

    public InventoryApiController(MongoDbContext context)
    {
        _context = context;
    }

    private async Task<(bool Authorized, string? PointId)> EffectivePointAsync(string? requested)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return (false, null);
        var actor = await _context.Employees.Find(e => e.Id == userId && e.IsActive).FirstOrDefaultAsync();
        if (actor == null) return (false, null);
        if (actor.Role == EmployeeRole.Seller || actor.Role == EmployeeRole.TradingPointManager)
        {
            if (string.IsNullOrWhiteSpace(actor.TradingPointId) ||
                (!string.IsNullOrWhiteSpace(requested) && requested != actor.TradingPointId))
                return (false, null);
            return (true, actor.TradingPointId);
        }
        return (true, requested);
    }

    // GET /api/inventory or GET /api/inventory?tradingPointId=...
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tradingPointId)
    {
        var scope = await EffectivePointAsync(tradingPointId);
        if (!scope.Authorized) return Forbid();
        var filter = string.IsNullOrWhiteSpace(scope.PointId)
            ? Builders<InventoryItem>.Filter.Empty
            : Builders<InventoryItem>.Filter.Eq(i => i.TradingPointId, scope.PointId);
        var items = await _context.InventoryItems.Find(filter).ToListAsync();
        return Ok(items);
    }

    // Low-stock alerts are management data, not seller checkout information.
    [Authorize(Roles = "Director,TradingPointManager,SupplyManager")]
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock([FromQuery] string? tradingPointId)
    {
        var scope = await EffectivePointAsync(tradingPointId);
        if (!scope.Authorized) return Forbid();
        var filter = string.IsNullOrWhiteSpace(scope.PointId)
            ? Builders<InventoryItem>.Filter.Empty
            : Builders<InventoryItem>.Filter.Eq(i => i.TradingPointId, scope.PointId);
        var items = await _context.InventoryItems.Find(filter).ToListAsync();
        return Ok(items.Where(i => i.Quantity <= i.MinimumQuantity).ToList());
    }
}
