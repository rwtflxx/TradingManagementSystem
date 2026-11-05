using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Repositories;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Infrastructure.Repositories;

/// <summary>MongoDB implementation of IProductRepository.</summary>
public sealed class MongoProductRepository : IProductRepository
{
    private readonly IMongoCollection<Product> _products;

    public MongoProductRepository(MongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _products = context.Products;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
        => await _products.Find(_ => true).ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default)
        => await _products.Find(Builders<Product>.Filter.In(p => p.Id, ids)).ToListAsync(ct);

    public async Task<Product?> GetByIdAsync(string id, CancellationToken ct = default)
        => await _products.Find(p => p.Id == id).FirstOrDefaultAsync(ct);

    public async Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default)
        => await _products.Find(p => p.Sku == sku).AnyAsync(ct);

    public Task AddAsync(Product product, CancellationToken ct = default)
        => _products.InsertOneAsync(product, cancellationToken: ct);

    public Task ReplaceAsync(Product product, CancellationToken ct = default)
        => _products.ReplaceOneAsync(p => p.Id == product.Id, product, cancellationToken: ct);

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
        => (await _products.DeleteOneAsync(p => p.Id == id, ct)).DeletedCount > 0;
}
