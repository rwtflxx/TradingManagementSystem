using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models.Api;

namespace TradingManagementSystem.Web.Controllers.Api;

[ApiController]
[Route("api/products")]
public class ProductsApiController : ControllerBase
{
    private readonly MongoDbContext _context;

    public ProductsApiController(MongoDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
    {
        var products = await _context.Products
            .Find(_ => true)
            .ToListAsync();

        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetById(string id)
    {
        var product = await _context.Products
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

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

        var existingProduct = await _context.Products
            .Find(p => p.Sku == request.Sku)
            .FirstOrDefaultAsync();

        if (existingProduct != null)
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

        await _context.Products.InsertOneAsync(product);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            product);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> Update(
        string id,
        UpdateProductRequest request)
    {
        var product = await _context.Products
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

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

        await _context.Products.ReplaceOneAsync(
            p => p.Id == id,
            product);

        return Ok(product);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _context.Products
            .DeleteOneAsync(p => p.Id == id);

        if (result.DeletedCount == 0)
        {
            return NotFound();
        }

        return NoContent();
    }
}