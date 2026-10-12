using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers.Api
{
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
                return NotFound();

            return Ok(product);
        }
    }
}