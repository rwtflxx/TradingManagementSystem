using System;
using System.Collections.Generic;
using System.Text;

using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;

namespace TradingManagementSystem.Infrastructure.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(
        string connectionString,
        string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<Employee> Employees =>
        _database.GetCollection<Employee>("employees");

    public IMongoCollection<TradingPoint> TradingPoints =>
        _database.GetCollection<TradingPoint>("tradingPoints");

    public IMongoCollection<Product> Products =>
        _database.GetCollection<Product>("products");

    public IMongoCollection<Supplier> Suppliers =>
        _database.GetCollection<Supplier>("suppliers");

    public IMongoCollection<InventoryItem> InventoryItems =>
        _database.GetCollection<InventoryItem>("inventoryItems");

    public IMongoCollection<Purchase> Purchases =>
        _database.GetCollection<Purchase>("purchases");

    public IMongoCollection<Receipt> Receipts =>
        _database.GetCollection<Receipt>("receipts");

    public IMongoCollection<Sale> Sales =>
        _database.GetCollection<Sale>("sales");
}