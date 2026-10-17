
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models.Api;

namespace TradingManagementSystem.Web.Controllers.Api;

[ApiController]
[Route("api/suppliers")]
public class SuppliersApiController : ControllerBase
{
    private readonly MongoDbContext _context;

    public SuppliersApiController(MongoDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Supplier>>> GetAll()
    {
        var suppliers = await _context.Suppliers
            .Find(_ => true)
            .ToListAsync();

        return Ok(suppliers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Supplier>> GetById(string id)
    {
        var supplier = await _context.Suppliers
            .Find(s => s.Id == id)
            .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return NotFound();
        }

        return Ok(supplier);
    }

    [HttpPost]
    public async Task<ActionResult<Supplier>> Create(
        CreateSupplierRequest request)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid().ToString()
        };

        try
        {
            supplier.UpdateData(
                request.Name,
                request.Phone,
                request.Email,
                request.Address);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        await _context.Suppliers.InsertOneAsync(supplier);

        return CreatedAtAction(
            nameof(GetById),
            new { id = supplier.Id },
            supplier);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Supplier>> Update(
        string id,
        UpdateSupplierRequest request)
    {
        var supplier = await _context.Suppliers
            .Find(s => s.Id == id)
            .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return NotFound();
        }

        try
        {
            supplier.UpdateData(
                request.Name,
                request.Phone,
                request.Email,
                request.Address);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        await _context.Suppliers.ReplaceOneAsync(
            s => s.Id == id,
            supplier);

        return Ok(supplier);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var hasPurchases = await _context.Purchases
            .Find(p => p.SupplierId == id)
            .AnyAsync();

        if (hasPurchases)
        {
            return Conflict(
                "Cannot delete a supplier with existing purchases.");
        }

        var result = await _context.Suppliers
            .DeleteOneAsync(s => s.Id == id);

        if (result.DeletedCount == 0)
        {
            return NotFound();
        }

        return NoContent();
    }
}
