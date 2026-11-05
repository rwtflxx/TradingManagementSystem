using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models;

namespace TradingManagementSystem.Web.Controllers;

[Authorize(Roles = "Director,SupplyManager,TradingPointManager")]
public sealed class SmartInventoryController : Controller
{
    private readonly MongoDbContext _db;
    private readonly SmartInventoryService _smart;
    public SmartInventoryController(MongoDbContext db, SmartInventoryService smart)
    {
        _db = db;
        _smart = smart;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actor = await _db.Employees.Find(x => x.Id == userId && x.IsActive).FirstOrDefaultAsync();
        if (actor == null) return Unauthorized();

        var stockFilter = actor.Role == EmployeeRole.TradingPointManager
            ? Builders<InventoryItem>.Filter.Eq(x => x.TradingPointId, actor.TradingPointId)
            : Builders<InventoryItem>.Filter.Empty;
        var salesFilter = actor.Role == EmployeeRole.TradingPointManager
            ? Builders<Sale>.Filter.Eq(x => x.TradingPointId, actor.TradingPointId)
            : Builders<Sale>.Filter.Empty;
        var stock = await _db.InventoryItems.Find(stockFilter).ToListAsync();
        var sales = await _db.Sales.Find(salesFilter).ToListAsync();
        var products = await _db.Products.Find(_ => true).ToListAsync();
        var points = await _db.TradingPoints.Find(_ => true).ToListAsync();
        var productNames = products.ToDictionary(x => x.Id, x => x.Name);
        var pointNames = points.ToDictionary(x => x.Id, x => x.Name);
        var model = new SmartInventoryPageModel();
        foreach (var recommendation in _smart.Analyze(stock, sales, DateTime.UtcNow))
        {
            model.Rows.Add(new SmartInventoryRow
            {
                Recommendation = recommendation,
                ProductName = productNames.GetValueOrDefault(recommendation.ProductId, recommendation.ProductId),
                PointName = pointNames.GetValueOrDefault(recommendation.TradingPointId, recommendation.TradingPointId),
                SourcePointName = recommendation.SuggestedTransferFromId == null ? null :
                    pointNames.GetValueOrDefault(recommendation.SuggestedTransferFromId)
            });
        }
        return View(model);
    }
}
