using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingManagementSystem.Domain.Enums;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Repositories;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models.Api;

namespace TradingManagementSystem.Web.Controllers.Api;

[ApiController]
[Route("api/products")]
public class ProductsApiController : ControllerBase
{
    private readonly MongoDbContext _context;
    private readonly IProductRepository _products;

    public ProductsApiController(MongoDbContext context, IProductRepository products)
    {
        _context = context;
        _products = products;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        if (actor.Role == EmployeeRole.Seller)
        {
            if (string.IsNullOrWhiteSpace(actor.TradingPointId)) return Forbid();
            var stock = await _context.InventoryItems.Find(i => i.TradingPointId == actor.TradingPointId && i.Quantity > 0).ToListAsync();
            var ids = stock.Select(i => i.ProductId).Distinct().ToList();
            return Ok(await _products.GetByIdsAsync(ids));
        }
        return Ok(await _products.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetById(string id)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        if (actor.Role == EmployeeRole.Seller)
        {
            if (string.IsNullOrWhiteSpace(actor.TradingPointId)) return Forbid();
            if (!await _context.InventoryItems.Find(i => i.ProductId == id && i.TradingPointId == actor.TradingPointId && i.Quantity > 0).AnyAsync())
                return NotFound();
        }
        var product = await _products.GetByIdAsync(id);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    private async Task<Employee?> CurrentAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _context.Employees.Find(x => x.Id == id && x.IsActive).FirstOrDefaultAsync();
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SupplyManager")]
    [HttpPost]
    public async Task<ActionResult<Product>> Create(
        CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Product name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            return BadRequest("SKU cannot be empty.");
        }

        if (request.SellingPrice < 0)
        {
            return BadRequest("Selling price cannot be negative.");
        }

        if (await _products.ExistsBySkuAsync(request.Sku))
        {
            return Conflict("A product with this SKU already exists.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Sku = request.Sku
        };

        product.UpdateData(
            request.Name,
            request.Category,
            request.SellingPrice,
            request.Description);

        await _products.AddAsync(product);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            product);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SupplyManager")]
    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> Update(
        string id,
        UpdateProductRequest request)
    {
        var product = await _products.GetByIdAsync(id);

        if (product == null)
        {
            return NotFound();
        }

        try
        {
            product.UpdateData(
                request.Name,
                request.Category,
                request.SellingPrice,
                request.Description);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        await _products.ReplaceAsync(product);

        return Ok(product);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SupplyManager")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _products.DeleteAsync(id))
        {
            return NotFound();
        }

        return NoContent();
    }
}
