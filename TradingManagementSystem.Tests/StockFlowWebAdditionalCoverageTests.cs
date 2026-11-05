// StockFlow lab 4: additional MVC integration scenarios.
// Reuses the existing isolated StockFlowWebFactory (stockflow_webtests_* database).
// No production MongoDB collections or C# controllers are modified by this file.
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using Xunit;

namespace TradingManagementSystem.Tests;

[Collection("StockFlow Web Integration")]
public sealed class StockFlowWebAdditionalCoverageTests
{
    private readonly StockFlowWebFactory _factory;
    public StockFlowWebAdditionalCoverageTests(StockFlowWebFactory factory) => _factory = factory;

    [Theory]
    [InlineData("sf-web-director", "/Catalog/Products")]
    [InlineData("sf-web-supply", "/Catalog/Products")]
    [InlineData("sf-web-seller-a", "/Catalog/Products")]
    [InlineData("sf-web-director", "/Catalog/Suppliers")]
    [InlineData("sf-web-supply", "/Catalog/Suppliers")]
    [InlineData("sf-web-director", "/Catalog/TradingPoints")]
    [InlineData("sf-web-manager-a", "/Catalog/TradingPoints")]
    [InlineData("sf-web-seller-a", "/Catalog/TradingPoints")]
    [InlineData("sf-web-director", "/Catalog/Purchases")]
    [InlineData("sf-web-manager-a", "/Catalog/Purchases")]
    [InlineData("sf-web-supply", "/Catalog/Purchases")]
    [InlineData("sf-web-director", "/Catalog/Receipts")]
    [InlineData("sf-web-supply", "/Catalog/Receipts")]
    [InlineData("sf-web-director", "/Catalog/Sales")]
    [InlineData("sf-web-manager-a", "/Catalog/Sales")]
    [InlineData("sf-web-seller-a", "/Catalog/Sales")]
    [InlineData("sf-web-director", "/Catalog/Inventory")]
    [InlineData("sf-web-manager-a", "/Catalog/Inventory")]
    [InlineData("sf-web-seller-a", "/Catalog/Inventory")]
    [InlineData("sf-web-director", "/Profile")]
    [InlineData("sf-web-manager-a", "/Profile")]
    [InlineData("sf-web-seller-a", "/Profile")]
    [InlineData("sf-web-director", "/Employees/Create")]
    [InlineData("sf-web-manager-a", "/Employees/Create")]
    [InlineData("sf-web-director", "/Employees/Edit/sf-web-seller-a")]
    [InlineData("sf-web-manager-a", "/Employees/Edit/sf-web-seller-a")]
    public async Task PermittedMvcPages_ReturnOk(string user, string url)
    {
        using var http = _factory.Browser(user);
        using var response = await http.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("sf-web-seller-a", "/Catalog/Suppliers")]
    [InlineData("sf-web-manager-a", "/Catalog/Suppliers")]
    [InlineData("sf-web-seller-a", "/Catalog/Receipts")]
    [InlineData("sf-web-seller-a", "/Catalog/Purchases")]
    [InlineData("sf-web-supply", "/Catalog/Sales")]
    [InlineData("sf-web-supply", "/Employees/Create")]
    [InlineData("sf-web-manager-b", "/Employees/Edit/sf-web-seller-a")]
    public async Task ForbiddenMvcPages_EnforceServerRoles(string user, string url)
    {
        using var http = _factory.Browser(user);
        using var response = await http.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StoreManager_OnlySeesOwnPointInCatalog()
    {
        using var http = _factory.Browser("sf-web-manager-a");
        var html = await http.GetStringAsync("/Catalog/TradingPoints");
        Assert.Contains("Test store A", html);
        Assert.DoesNotContain("Test store B", html);
    }

    [Fact]
    public async Task StoreManager_OnlySeesOwnPurchasesInCatalog()
    {
        using var http = _factory.Browser("sf-web-manager-a");
        var html = await http.GetStringAsync("/Catalog/Purchases");
        Assert.Contains(StockFlowWebFactory.PurchaseA, html);
        Assert.DoesNotContain(StockFlowWebFactory.PurchaseB, html);
    }

    [Fact]
    public async Task Seller_OnlySeesOwnSalesInCatalog()
    {
        using var http = _factory.Browser("sf-web-seller-a");
        var html = await http.GetStringAsync("/Catalog/Sales");
        Assert.Contains(StockFlowWebFactory.SaleA, html);
        Assert.DoesNotContain(StockFlowWebFactory.SaleB, html);
    }

    [Fact]
    public async Task Seller_OnlySeesAvailableProductsInCatalog()
    {
        using var http = _factory.Browser("sf-web-seller-a");
        var html = await http.GetStringAsync("/Catalog/Products");
        Assert.Contains("Keyboard A", html);
        Assert.DoesNotContain("Mouse B", html);
    }

    [Fact]
    public async Task StoreManager_CannotEditSellerFromAnotherPoint()
    {
        using var http = _factory.Browser("sf-web-manager-a");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await http.GetAsync("/Employees/Edit/sf-web-seller-b")).StatusCode);
    }

    [Fact]
    public async Task MissingEmployee_EditReturnsNotFound()
    {
        using var http = _factory.Browser("sf-web-director");
        Assert.Equal(HttpStatusCode.NotFound,
            (await http.GetAsync("/Employees/Edit/no-such-employee")).StatusCode);
    }

    [Fact]
    public async Task HomePrivacyRedirectsToDashboard()
    {
        using var http = _factory.Browser("sf-web-director");
        Assert.Equal(HttpStatusCode.Redirect,
            (await http.GetAsync("/Home/Privacy")).StatusCode);
    }

    [Fact]
    public async Task HomeErrorPageDisplaysWithoutThrowing()
    {
        using var http = _factory.Browser("sf-web-director");
        Assert.Equal(HttpStatusCode.OK,
            (await http.GetAsync("/Home/Error")).StatusCode);
    }

    [Fact]
    public async Task SignedInUserIsRedirectedFromLoginForm()
    {
        using var http = _factory.Browser("sf-web-director");
        Assert.Equal(HttpStatusCode.Redirect,
            (await http.GetAsync("/Account/Login")).StatusCode);
    }

    [Fact]
    public async Task InvalidLoginCannotAuthenticate()
    {
        using var http = _factory.Browser();
        using var response = await SubmitFormAsync(http, "/Account/Login", "/Account/Login", new()
        {
            ["Login"] = "not-a-real-login", ["Password"] = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Невірний логін або пароль", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task ProfileRejectsDuplicateLogin()
    {
        using var http = _factory.Browser("sf-web-director");
        using var response = await SubmitFormAsync(http, "/Profile", "/Profile/UpdateDetails", new()
        {
            ["Details.FirstName"] = "Test",
            ["Details.LastName"] = "Director",
            ["Details.Login"] = "sf-web-seller-a"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Цей логін уже зайнятий", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task ProfileRejectsIncorrectCurrentPassword()
    {
        using var http = _factory.Browser("sf-web-director");
        using var response = await SubmitFormAsync(http, "/Profile", "/Profile/UpdatePassword", new()
        {
            ["Password.CurrentPassword"] = "wrong-password",
            ["Password.NewPassword"] = "newpassword12345",
            ["Password.ConfirmPassword"] = "newpassword12345"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Неправильний поточний пароль", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task ManagerCannotCreateDirectorAccount()
    {
        using var http = _factory.Browser("sf-web-manager-a");
        var login = "deny" + Guid.NewGuid().ToString("N")[..15];
        using var response = await SubmitFormAsync(http, "/Employees/Create", "/Employees/Create", new()
        {
            ["FirstName"] = "New", ["LastName"] = "Worker", ["Login"] = login,
            ["Password"] = "password12345", ["Role"] = "Director",
            ["TradingPointId"] = StockFlowWebFactory.PointA
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var count = await _factory.Database.GetCollection<Employee>("employees")
            .CountDocumentsAsync(x => x.Login == login);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task PurchaseFormRejectsMissingSupplier()
    {
        using var http = _factory.Browser("sf-web-supply");
        using var response = await SubmitFormAsync(http, "/Commerce/NewPurchase", "/Commerce/NewPurchase", new()
        {
            ["SupplierId"] = "", ["TradingPointId"] = StockFlowWebFactory.PointA,
            ["Items[0].ProductId"] = StockFlowWebFactory.ProductA,
            ["Items[0].Quantity"] = "2", ["Items[0].PurchasePrice"] = "40"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Вибери постачальника", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task PurchaseFormValidSubmissionDoesNotChangeStock()
    {
        using var http = _factory.Browser("sf-web-supply");
        var stock = _factory.Database.GetCollection<InventoryItem>("inventoryItems");
        var before = await stock.Find(x => x.Id == "sf-stock-a").FirstAsync();
        using var response = await SubmitFormAsync(http, "/Commerce/NewPurchase", "/Commerce/NewPurchase", new()
        {
            ["SupplierId"] = StockFlowWebFactory.SupplierA,
            ["TradingPointId"] = StockFlowWebFactory.PointA,
            ["Items[0].ProductId"] = StockFlowWebFactory.ProductA,
            ["Items[0].Quantity"] = "2", ["Items[0].PurchasePrice"] = "40"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var after = await stock.Find(x => x.Id == "sf-stock-a").FirstAsync();
        Assert.Equal(before.Quantity, after.Quantity);
    }

    [Fact]
    public async Task ReceiptFormRejectsMissingPurchase()
    {
        using var http = _factory.Browser("sf-web-supply");
        using var response = await SubmitFormAsync(http, "/Commerce/RegisterReceipt", "/Commerce/RegisterReceipt", new()
        {
            ["PurchaseId"] = "no-such-purchase"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Закупівлю не знайдено", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task SaleFormRejectsInvalidRequestId()
    {
        using var http = _factory.Browser("sf-web-seller-a");
        using var response = await SubmitFormAsync(http, "/Commerce/NewSale", "/Commerce/NewSale", new()
        {
            ["RequestId"] = "invalid-guid", ["PaymentMethod"] = "0",
            ["Items[0].ProductId"] = StockFlowWebFactory.ProductA,
            ["Items[0].Quantity"] = "1"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Некоректні дані операції", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task SaleFormRejectsNegativeQuantity()
    {
        using var http = _factory.Browser("sf-web-seller-a");
        using var response = await SubmitFormAsync(http, "/Commerce/NewSale", "/Commerce/NewSale", new()
        {
            ["RequestId"] = Guid.NewGuid().ToString(), ["PaymentMethod"] = "0",
            ["Items[0].ProductId"] = StockFlowWebFactory.ProductA,
            ["Items[0].Quantity"] = "-2"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("додатною кількістю", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task CatalogRejectsInvalidProductCreation()
    {
        using var http = _factory.Browser("sf-web-supply");
        using var response = await SubmitFormAsync(http, "/Catalog/Products", "/Catalog/AddProduct", new()
        {
            ["name"] = "Test", ["sku"] = "", ["category"] = "Test",
            ["sellingPrice"] = "20", ["description"] = "Invalid SKU"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task CatalogRejectsInvalidSupplier()
    {
        using var http = _factory.Browser("sf-web-supply");
        using var response = await SubmitFormAsync(http, "/Catalog/Suppliers", "/Catalog/AddSupplier", new()
        {
            ["name"] = "", ["phone"] = "", ["email"] = "", ["address"] = ""
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task CatalogRejectsInvalidTradingPoint()
    {
        using var http = _factory.Browser("sf-web-director");
        using var response = await SubmitFormAsync(http, "/Catalog/TradingPoints", "/Catalog/AddTradingPoint", new()
        {
            ["name"] = "", ["address"] = ""
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SubmitFormAsync(HttpClient http,
        string getUrl, string postUrl, Dictionary<string, string> values)
    {
        using var page = await http.GetAsync(getUrl);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var markup = await page.Content.ReadAsStringAsync();
        var token = Regex.Match(markup,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(token.Success, "Antiforgery input was not found on " + getUrl);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return await http.PostAsync(postUrl, new FormUrlEncodedContent(values));
    }
}
