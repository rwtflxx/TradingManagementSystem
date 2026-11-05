// Integration tests for StockFlow Web. Runs against a uniquely named, temporary MongoDB database.
// The test-only authentication scheme is registered ONLY in this test host.
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Web.Controllers;
using TradingManagementSystem.Web.Models;
using TradingManagementSystem.Web.Models.Api;
using Xunit;

namespace TradingManagementSystem.Tests;

public sealed class StockFlowTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeId = "StockFlowTestOnly";

    public StockFlowTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-StockFlow-User", out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var id = values.ToString();
        (string role, string? point) = id switch
        {
            "sf-web-director" => ("Director", null),
            "sf-web-supply" => ("SupplyManager", null),
            "sf-web-manager-a" => ("TradingPointManager", StockFlowWebFactory.PointA),
            "sf-web-manager-b" => ("TradingPointManager", StockFlowWebFactory.PointB),
            "sf-web-seller-a" => ("Seller", StockFlowWebFactory.PointA),
            "sf-web-seller-b" => ("Seller", StockFlowWebFactory.PointB),
            _ => (string.Empty, null)
        };
        if (role.Length == 0) return Task.FromResult(AuthenticateResult.Fail("Unknown test account"));
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Name, "Web Test User"),
            new Claim(ClaimTypes.Role, role),
            new Claim("sf.point", point ?? string.Empty)
        }, SchemeId);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeId)));
    }
}

public sealed class StockFlowWebFactory : WebApplicationFactory<HomeController>
{
    public const string PointA = "sf-web-point-a";
    public const string PointB = "sf-web-point-b";
    public const string ProductA = "sf-web-product-a";
    public const string ProductB = "sf-web-product-b";
    public const string SupplierA = "sf-web-supplier-a";
    public const string PurchaseA = "sf-web-purchase-a";
    public const string PurchaseB = "sf-web-purchase-b";
    public const string SaleA = "sf-web-sale-a";
    public const string SaleB = "sf-web-sale-b";

    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("STOCKFLOW_TEST_MONGO_URI") ?? "mongodb://127.0.0.1:27017";
    public string TemporaryDatabase { get; } = "stockflow_webtests_" + Guid.NewGuid().ToString("N");
    private readonly MongoClient _client;
    public IMongoDatabase Database { get; }

    public StockFlowWebFactory()
    {
        _client = new MongoClient(_connectionString);
        // Failure here should be explicit. Existing production DB is never used.
        _client.GetDatabase("admin").RunCommand<MongoDB.Bson.BsonDocument>(
            new MongoDB.Bson.BsonDocument("ping", 1));
        Database = _client.GetDatabase(TemporaryDatabase);
        Seed();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Explicit content root works with .slnx-only solutions, too.
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "TradingManagementSystem.Web"));
        builder.UseContentRoot(webRoot);
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = _connectionString,
                ["MongoDb:DatabaseName"] = TemporaryDatabase
            }));
        builder.ConfigureTestServices(services =>
        {
            // The Web app creates its MongoDbContext before test configuration overrides are applied.
            // Replace that registration to isolate all HTTP tests from the real StockFlow database.
            services.RemoveAll<TradingManagementSystem.Infrastructure.Data.MongoDbContext>();
            services.AddSingleton(new TradingManagementSystem.Infrastructure.Data.MongoDbContext(_connectionString, TemporaryDatabase));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = StockFlowTestAuthHandler.SchemeId;
                options.DefaultChallengeScheme = StockFlowTestAuthHandler.SchemeId;
                options.DefaultForbidScheme = StockFlowTestAuthHandler.SchemeId;
            }).AddScheme<AuthenticationSchemeOptions, StockFlowTestAuthHandler>(
                StockFlowTestAuthHandler.SchemeId, _ => { });
        });
    }

    public HttpClient Browser(string? employee = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        if (employee is not null)
            client.DefaultRequestHeaders.Add("X-Test-StockFlow-User", employee);
        return client;
    }

    private void Seed()
    {
        Database.GetCollection<TradingPoint>("tradingPoints").InsertMany(new[]
        {
            new TradingPoint { Id = PointA, Name = "Test store A", Address = "Test street A" },
            new TradingPoint { Id = PointB, Name = "Test store B", Address = "Test street B" }
        });
        Database.GetCollection<Employee>("employees").InsertMany(new[]
        {
            EmployeeFor("sf-web-director", EmployeeRole.Director),
            EmployeeFor("sf-web-supply", EmployeeRole.SupplyManager),
            EmployeeFor("sf-web-manager-a", EmployeeRole.TradingPointManager, PointA),
            EmployeeFor("sf-web-manager-b", EmployeeRole.TradingPointManager, PointB),
            EmployeeFor("sf-web-seller-a", EmployeeRole.Seller, PointA),
            EmployeeFor("sf-web-seller-b", EmployeeRole.Seller, PointB)
        });
        Database.GetCollection<Product>("products").InsertMany(new[]
        {
            new Product { Id = ProductA, Name = "Keyboard A", Sku = "WEB-A-001", Category = "Test", SellingPrice = 100m },
            new Product { Id = ProductB, Name = "Mouse B", Sku = "WEB-B-001", Category = "Test", SellingPrice = 200m }
        });
        Database.GetCollection<Supplier>("suppliers").InsertOne(
            new Supplier { Id = SupplierA, Name = "Test Supplier", Email = "test@example.org" });
        var stockA = new InventoryItem { Id = "sf-stock-a", ProductId = ProductA, TradingPointId = PointA, MinimumQuantity = 5 };
        stockA.Increase(10);
        var stockB = new InventoryItem { Id = "sf-stock-b", ProductId = ProductB, TradingPointId = PointB, MinimumQuantity = 5 };
        stockB.Increase(3);
        Database.GetCollection<InventoryItem>("inventoryItems").InsertMany(new[] { stockA, stockB });
        Database.GetCollection<Purchase>("purchases").InsertMany(new[]
        {
            PurchaseFor(PurchaseA, PointA, ProductA), PurchaseFor(PurchaseB, PointB, ProductB)
        });
        Database.GetCollection<Sale>("sales").InsertMany(new[]
        {
            SaleFor(SaleA, "sf-web-seller-a", PointA, ProductA),
            SaleFor(SaleB, "sf-web-seller-b", PointB, ProductB)
        });
    }

    private static Employee EmployeeFor(string id, EmployeeRole role, string? point = null)
    {
        var employee = new Employee
        {
            Id = id, Login = id, FirstName = "Test", LastName = "User", Role = role,
            TradingPointId = point
        };
        employee.ChangePassword(new Microsoft.AspNetCore.Identity.PasswordHasher<Employee>().HashPassword(employee, "StockFlowTestPassword123!"));
        return employee;
    }

    private static Purchase PurchaseFor(string id, string point, string product)
    {
        var purchase = new Purchase { Id = id, SupplierId = SupplierA, TradingPointId = point };
        purchase.AddItem(new PurchaseItem { Id = Guid.NewGuid().ToString(), ProductId = product, Quantity = 2, PurchasePrice = 50 });
        return purchase;
    }

    private static Sale SaleFor(string id, string seller, string point, string product)
    {
        var sale = new Sale { Id = id, SellerId = seller, TradingPointId = point, PaymentMethod = PaymentMethod.Cash };
        sale.AddItem(new SaleItem { Id = Guid.NewGuid().ToString(), ProductId = product, Quantity = 1, UnitPrice = 100 });
        sale.Complete();
        return sale;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Safety guard: never drop a normal StockFlow database.
            if (!TemporaryDatabase.StartsWith("stockflow_webtests_", StringComparison.Ordinal)
                || TemporaryDatabase.Length < 40)
                throw new InvalidOperationException("Invalid temporary database name; refusing cleanup.");
            _client.DropDatabase(TemporaryDatabase);
        }
        base.Dispose(disposing);
    }
}

[CollectionDefinition("StockFlow Web Integration", DisableParallelization = true)]
public sealed class StockFlowWebCollection : ICollectionFixture<StockFlowWebFactory> { }

[Collection("StockFlow Web Integration")]
public sealed class StockFlowWebIntegrationTests
{
    private readonly StockFlowWebFactory _factory;
    public StockFlowWebIntegrationTests(StockFlowWebFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/inventory")]
    [InlineData("/api/sales")]
    [InlineData("/api/purchases")]
    [InlineData("/Home/Index")]
    public async Task Anonymous_CannotOpenProtectedPages(string url)
    {
        using var http = _factory.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task LoginScreen_AllowsAnonymousVisitors()
    {
        using var http = _factory.Browser();
        using var response = await http.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FirstRunSetup_DoesNotRunInProduction()
    {
        using var http = _factory.Browser();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/Setup")).StatusCode);
    }

    [Theory]
    [InlineData("sf-web-director", "/Home/Index", 200)]
    [InlineData("sf-web-supply", "/Commerce/NewPurchase", 200)]
    [InlineData("sf-web-supply", "/Commerce/RegisterReceipt", 200)]
    [InlineData("sf-web-supply", "/SmartInventory", 200)]
    [InlineData("sf-web-manager-a", "/SmartInventory", 200)]
    [InlineData("sf-web-seller-a", "/Commerce/NewSale", 200)]
    [InlineData("sf-web-manager-a", "/Employees", 200)]
    [InlineData("sf-web-director", "/Employees", 200)]
    [InlineData("sf-web-seller-a", "/SmartInventory", 403)]
    [InlineData("sf-web-director", "/Commerce/NewSale", 403)]
    [InlineData("sf-web-manager-a", "/Commerce/NewPurchase", 403)]
    [InlineData("sf-web-seller-a", "/Employees", 403)]
    [InlineData("sf-web-supply", "/Employees", 403)]
    public async Task WebPages_RespectRoles(string employee, string url, int expectedStatus)
    {
        using var http = _factory.Browser(employee);
        using var response = await http.GetAsync(url);
        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
    }

    [Theory]
    [InlineData("sf-web-director", "/api/suppliers", 200)]
    [InlineData("sf-web-supply", "/api/suppliers", 200)]
    [InlineData("sf-web-manager-a", "/api/suppliers", 403)]
    [InlineData("sf-web-seller-a", "/api/suppliers", 403)]
    [InlineData("sf-web-supply", "/api/purchases", 200)]
    [InlineData("sf-web-manager-a", "/api/purchases", 200)]
    [InlineData("sf-web-seller-a", "/api/purchases", 403)]
    [InlineData("sf-web-seller-a", "/api/receipts", 403)]
    [InlineData("sf-web-seller-a", "/api/inventory/low-stock", 403)]
    [InlineData("sf-web-director", "/api/inventory/low-stock", 200)]
    [InlineData("sf-web-director", "/api/tradingpoints", 200)]
    public async Task ApiRoutes_RespectRoles(string employee, string url, int expectedStatus)
    {
        using var http = _factory.Browser(employee);
        Assert.Equal((HttpStatusCode)expectedStatus, (await http.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Seller_OnlySeesProductsAvailableAtOwnPoint()
    {
        using var http = _factory.Browser("sf-web-seller-a");
        var body = await http.GetStringAsync("/api/products");
        Assert.Contains(StockFlowWebFactory.ProductA, body);
        Assert.DoesNotContain(StockFlowWebFactory.ProductB, body);
        Assert.Equal(HttpStatusCode.NotFound,
            (await http.GetAsync("/api/products/" + StockFlowWebFactory.ProductB)).StatusCode);
    }

    [Fact]
    public async Task Manager_OnlySeesPurchasesAtAssignedStore()
    {
        using var http = _factory.Browser("sf-web-manager-a");
        var body = await http.GetStringAsync("/api/purchases");
        Assert.Contains(StockFlowWebFactory.PurchaseA, body);
        Assert.DoesNotContain(StockFlowWebFactory.PurchaseB, body);
        Assert.Equal(HttpStatusCode.NotFound,
            (await http.GetAsync("/api/purchases/" + StockFlowWebFactory.PurchaseB)).StatusCode);
    }

    [Fact]
    public async Task Seller_OnlySeesOwnSales()
    {
        using var http = _factory.Browser("sf-web-seller-a");
        var body = await http.GetStringAsync("/api/sales");
        Assert.Contains(StockFlowWebFactory.SaleA, body);
        Assert.DoesNotContain(StockFlowWebFactory.SaleB, body);
        Assert.Equal(HttpStatusCode.NotFound,
            (await http.GetAsync("/api/sales/" + StockFlowWebFactory.SaleB)).StatusCode);
    }

    [Fact]
    public async Task Manager_OnlySeesSalesAtOwnStore()
    {
        using var http = _factory.Browser("sf-web-manager-a");
        var body = await http.GetStringAsync("/api/sales");
        Assert.Contains(StockFlowWebFactory.SaleA, body);
        Assert.DoesNotContain(StockFlowWebFactory.SaleB, body);
    }

    [Fact]
    public async Task Seller_CannotReadAnotherStoresInventory()
    {
        using var http = _factory.Browser("sf-web-seller-a");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await http.GetAsync("/api/inventory?tradingPointId=" + StockFlowWebFactory.PointB)).StatusCode);
        var body = await http.GetStringAsync("/api/inventory");
        Assert.Contains(StockFlowWebFactory.ProductA, body);
        Assert.DoesNotContain(StockFlowWebFactory.ProductB, body);
    }

    [Fact]
    public async Task SupplierManager_CanCreateProduct_ButSellerCannot()
    {
        using var supply = _factory.Browser("sf-web-supply");
        using var seller = _factory.Browser("sf-web-seller-a");
        var request = new CreateProductRequest
        {
            Name = "Fresh " + Guid.NewGuid().ToString("N"),
            Sku = "WEB-" + Guid.NewGuid().ToString("N"),
            Category = "Test", SellingPrice = 123.45m
        };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await seller.PostAsJsonAsync("/api/products", request)).StatusCode);
        var created = await supply.PostAsJsonAsync("/api/products", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await supply.PostAsJsonAsync("/api/products", request)).StatusCode);
    }

    [Fact]
    public async Task SupplierManager_CannotCreateInvalidProduct()
    {
        using var http = _factory.Browser("sf-web-supply");
        var response = await http.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Test", Sku = "", SellingPrice = -1
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SupplierManager_CanCreatePurchaseWithoutChangingStock()
    {
        using var http = _factory.Browser("sf-web-supply");
        var before = await _factory.Database.GetCollection<InventoryItem>("inventoryItems")
            .Find(x => x.ProductId == StockFlowWebFactory.ProductA && x.TradingPointId == StockFlowWebFactory.PointA)
            .FirstAsync();
        var amount = before.Quantity;
        var create = await http.PostAsJsonAsync("/api/purchases", new
        {
            supplierId = StockFlowWebFactory.SupplierA,
            tradingPointId = StockFlowWebFactory.PointA,
            items = new[] { new { productId = StockFlowWebFactory.ProductA, quantity = 2, purchasePrice = 50m } }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var after = await _factory.Database.GetCollection<InventoryItem>("inventoryItems")
            .Find(x => x.ProductId == StockFlowWebFactory.ProductA && x.TradingPointId == StockFlowWebFactory.PointA)
            .FirstAsync();
        Assert.Equal(amount, after.Quantity);
    }

    [Fact]
    public async Task RegisterReceipt_AddsStockInTemporaryDatabase()
    {
        using var http = _factory.Browser("sf-web-supply");
        var product = "sf-temp-" + Guid.NewGuid().ToString("N");
        await _factory.Database.GetCollection<Product>("products").InsertOneAsync(new Product
        {
            Id = product, Name = "Receipt Test", Sku = product, SellingPrice = 20m
        });
        var purchase = new Purchase
        {
            Id = "sf-temp-purchase-" + Guid.NewGuid().ToString("N"),
            SupplierId = StockFlowWebFactory.SupplierA,
            TradingPointId = StockFlowWebFactory.PointA
        };
        purchase.AddItem(new PurchaseItem { Id = Guid.NewGuid().ToString(), ProductId = product, Quantity = 4, PurchasePrice = 10m });
        await _factory.Database.GetCollection<Purchase>("purchases").InsertOneAsync(purchase);
        var response = await http.PostAsJsonAsync("/api/receipts", new { purchaseId = purchase.Id });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stock = await _factory.Database.GetCollection<InventoryItem>("inventoryItems")
            .Find(x => x.ProductId == product && x.TradingPointId == StockFlowWebFactory.PointA).FirstAsync();
        Assert.Equal(4, stock.Quantity);
        Assert.Equal(HttpStatusCode.Conflict,
            (await http.PostAsJsonAsync("/api/receipts", new { purchaseId = purchase.Id })).StatusCode);
    }

    [Fact]
    public async Task Seller_SaleDecrementsStockAndBlocksDuplicateRequest()
    {
        using var http = _factory.Browser("sf-web-seller-a");
        var product = "sf-temp-" + Guid.NewGuid().ToString("N");
        await _factory.Database.GetCollection<Product>("products").InsertOneAsync(new Product
        {
            Id = product, Name = "Sale Test", Sku = product, SellingPrice = 35m
        });
        var stock = new InventoryItem
        {
            Id = "sf-temp-stock-" + Guid.NewGuid().ToString("N"),
            ProductId = product, TradingPointId = StockFlowWebFactory.PointA, MinimumQuantity = 1
        };
        stock.Increase(6);
        await _factory.Database.GetCollection<InventoryItem>("inventoryItems").InsertOneAsync(stock);
        var id = Guid.NewGuid().ToString();
        var body = new
        {
            requestId = id, sellerId = "untrusted-value",
            tradingPointId = StockFlowWebFactory.PointA,
            paymentMethod = 0,
            items = new[] { new { productId = product, quantity = 2 } }
        };
        var response = await http.PostAsJsonAsync("/api/sales", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var remaining = await _factory.Database.GetCollection<InventoryItem>("inventoryItems")
            .Find(x => x.Id == stock.Id).FirstAsync();
        Assert.Equal(4, remaining.Quantity);
        var sale = await _factory.Database.GetCollection<Sale>("sales").Find(s => s.Id == id).FirstAsync();
        Assert.Equal("sf-web-seller-a", sale.SellerId);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/sales", body)).StatusCode);
    }

    [Fact]
    public async Task Seller_CannotCreateSaleAtOtherStore()
    {
        using var http = _factory.Browser("sf-web-seller-a");
        var result = await http.PostAsJsonAsync("/api/sales", new
        {
            requestId = Guid.NewGuid().ToString(),
            tradingPointId = StockFlowWebFactory.PointB,
            paymentMethod = 0,
            items = new[] { new { productId = StockFlowWebFactory.ProductB, quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }
}

public sealed class StockFlowWebModelValidationTests
{
    private static bool Valid(object item)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(item, new ValidationContext(item), results, true);
    }

    [Fact]
    public void LoginInput_RequiresLoginAndPassword() =>
        Assert.False(Valid(new LoginInput()));

    [Fact]
    public void LoginInput_AllowsPopulatedLoginAndPassword() =>
        Assert.True(Valid(new LoginInput { Login = "alice", Password = "password" }));

    [Fact]
    public void EmployeeInput_RejectsShortPassword() =>
        Assert.False(Valid(new EmployeeInput { FirstName = "A", LastName = "B", Login = "alice", Password = "short" }));

    [Fact]
    public void EmployeeInput_ValidatesGoodDetails() =>
        Assert.True(Valid(new EmployeeInput { FirstName = "Alice", LastName = "S", Login = "alice", Password = "longtestpassword" }));

    [Fact]
    public void ProfilePasswordInput_RejectsMismatchedConfirmation() =>
        Assert.False(Valid(new ProfilePasswordInput { CurrentPassword = "oldpassword12", NewPassword = "newpassword12", ConfirmPassword = "differentnew123" }));

    [Fact]
    public void ProfilePasswordInput_AllowsMatchingConfirmation() =>
        Assert.True(Valid(new ProfilePasswordInput { CurrentPassword = "oldpassword12", NewPassword = "newpassword12", ConfirmPassword = "newpassword12" }));

    [Fact]
    public void SupplierRequest_RejectsInvalidEmail() =>
        Assert.False(Valid(new CreateSupplierRequest { Name = "Supplier", Email = "not-an-email" }));

    [Fact]
    public void SupplierRequest_AcceptsValidEmail() =>
        Assert.True(Valid(new CreateSupplierRequest { Name = "Supplier", Email = "supplier@example.org" }));

    [Fact]
    public void SmartInventoryPageModel_ComputesEmptyStatistics()
    {
        var page = new SmartInventoryPageModel();
        Assert.Equal(0, page.Alerts);
        Assert.Equal(0, page.Critical);
    }
}
