
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingManagementSystem.Domain.Enums;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers.Api;

[ApiController]
[Route("api/tradingpoints")]
public class TradingPointsApiController : ControllerBase
{
    private readonly MongoDbContext _context;

    public TradingPointsApiController(MongoDbContext context)
    {
        _context = context;
    }

    private async Task<Employee?> CurrentAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _context.Employees.Find(e => e.Id == userId && e.IsActive).FirstOrDefaultAsync();
    }

    [HttpGet]
    public async Task<ActionResult<List<TradingPoint>>> GetAll()
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        var filter = actor.Role == EmployeeRole.Seller || actor.Role == EmployeeRole.TradingPointManager
            ? Builders<TradingPoint>.Filter.Eq(p => p.Id, actor.TradingPointId)
            : Builders<TradingPoint>.Filter.Empty;
        var points = await _context.TradingPoints.Find(filter).ToListAsync();

        return Ok(points);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TradingPoint>> GetById(string id)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        if ((actor.Role == EmployeeRole.Seller || actor.Role == EmployeeRole.TradingPointManager) && actor.TradingPointId != id)
            return NotFound();
        var point = await _context.TradingPoints.Find(p => p.Id == id).FirstOrDefaultAsync();

        if (point == null)
            return NotFound();

        return Ok(point);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Director")]
    [HttpPost]
    public async Task<ActionResult<TradingPoint>> Create(
        TradingPointRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Trading point name is required.");

        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest("Address is required.");

        var point = new TradingPoint
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name.Trim(),
            Address = request.Address.Trim()
        };

        await _context.TradingPoints.InsertOneAsync(point);

        return CreatedAtAction(
            nameof(GetById),
            new { id = point.Id },
            point);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Director")]
    [HttpPut("{id}")]
    public async Task<ActionResult<TradingPoint>> Update(
        string id,
        TradingPointRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Address))
            return BadRequest("Name and address are required.");

        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        if ((actor.Role == EmployeeRole.Seller || actor.Role == EmployeeRole.TradingPointManager) && actor.TradingPointId != id)
            return NotFound();
        var point = await _context.TradingPoints.Find(p => p.Id == id).FirstOrDefaultAsync();

        if (point == null)
            return NotFound();

        point.Name = request.Name.Trim();
        point.Address = request.Address.Trim();

        await _context.TradingPoints.ReplaceOneAsync(
            p => p.Id == id, point);

        return Ok(point);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Director")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var hasInventory = await _context.InventoryItems
            .Find(i => i.TradingPointId == id)
            .AnyAsync();

        var hasPurchases = await _context.Purchases
            .Find(p => p.TradingPointId == id)
            .AnyAsync();

        var hasSales = await _context.Sales
            .Find(s => s.TradingPointId == id)
            .AnyAsync();

        var hasAssignedEmployees = await _context.Employees
            .Find(e => e.TradingPointId == id).AnyAsync();
        if (hasInventory || hasPurchases || hasSales || hasAssignedEmployees)
            return Conflict(
                "Cannot delete a trading point with related records or assigned employees.");

        var result = await _context.TradingPoints
            .DeleteOneAsync(p => p.Id == id);

        if (result.DeletedCount == 0)
            return NotFound();

        return NoContent();
    }
}

public class TradingPointRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

