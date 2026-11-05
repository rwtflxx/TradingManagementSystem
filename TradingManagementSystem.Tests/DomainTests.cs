using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using Xunit;

namespace TradingManagementSystem.Tests;

public class ProductTests
{
    [Fact]
    public void UpdateData_SavesValidValues()
    {
        var product = new Product();
        product.UpdateData("Keyboard", "Accessories", 350m, "USB");
        Assert.Equal("Keyboard", product.Name);
        Assert.Equal("Accessories", product.Category);
        Assert.Equal(350m, product.SellingPrice);
        Assert.Equal("USB", product.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void UpdateData_RejectsEmptyName(string name)
    {
        var product = new Product { Name = "Original" };
        Assert.Throws<ArgumentException>(() => product.UpdateData(name, "A", 1, ""));
        Assert.Equal("Original", product.Name);
    }

    [Fact]
    public void UpdateData_RejectsNegativePrice()
    {
        var product = new Product { Name = "Original" };
        Assert.Throws<ArgumentException>(() => product.UpdateData("New", "A", -1m, ""));
        Assert.Equal("Original", product.Name);
    }

    [Fact]
    public void UpdateData_AllowsZeroPrice()
    {
        var product = new Product();
        product.UpdateData("Sample", "A", 0m, "");
        Assert.Equal(0m, product.SellingPrice);
    }
}

public class SupplierTests
{
    [Fact]
    public void UpdateData_TrimsValues()
    {
        var supplier = new Supplier();
        supplier.UpdateData("  Supplier  ", " 123 ", " sales@example.com ", "  Kyiv ");
        Assert.Equal("Supplier", supplier.Name);
        Assert.Equal("123", supplier.Phone);
        Assert.Equal("sales@example.com", supplier.Email);
        Assert.Equal("Kyiv", supplier.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateData_RejectsBlankName(string name)
    {
        Assert.Throws<ArgumentException>(() => new Supplier().UpdateData(name, "", "", ""));
    }

    [Fact]
    public void UpdateData_RejectsInvalidEmailWithoutChangingExistingData()
    {
        var supplier = new Supplier { Name = "Old" };
        Assert.Throws<ArgumentException>(() => supplier.UpdateData("New", "", "bad-email", ""));
        Assert.Equal("Old", supplier.Name);
    }

    [Fact]
    public void UpdateData_AllowsEmptyEmail()
    {
        var supplier = new Supplier();
        supplier.UpdateData("Name", "", "", "");
        Assert.Equal("", supplier.Email);
    }

    [Fact]
    public void UpdateData_HandlesNullOptionalValues()
    {
        var supplier = new Supplier();
        supplier.UpdateData("Name", null!, null!, null!);
        Assert.Equal(string.Empty, supplier.Phone);
        Assert.Equal(string.Empty, supplier.Email);
        Assert.Equal(string.Empty, supplier.Address);
    }
}

public class EmployeeTests
{
    [Fact]
    public void ChangePassword_UpdatesHash()
    {
        var employee = new Employee { Role = EmployeeRole.Director };
        employee.ChangePassword("hashed-password");
        Assert.Equal("hashed-password", employee.PasswordHash);
        Assert.Equal(EmployeeRole.Director, employee.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ChangePassword_RejectsBlankHash(string value)
    {
        Assert.Throws<ArgumentException>(() => new Employee().ChangePassword(value));
    }
}

public class InventoryItemTests
{
    [Fact]
    public void Increase_UpdatesQuantity()
    {
        var item = new InventoryItem();
        item.Increase(10);
        item.Increase(5);
        Assert.Equal(15, item.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Increase_RejectsNonPositiveQuantity(int quantity)
    {
        var item = new InventoryItem();
        Assert.Throws<ArgumentException>(() => item.Increase(quantity));
        Assert.Equal(0, item.Quantity);
    }

    [Fact]
    public void Decrease_UpdatesQuantity()
    {
        var item = new InventoryItem();
        item.Increase(10);
        item.Decrease(3);
        Assert.Equal(7, item.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Decrease_RejectsNonPositiveQuantity(int quantity)
    {
        var item = new InventoryItem();
        item.Increase(5);
        Assert.Throws<ArgumentException>(() => item.Decrease(quantity));
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void Decrease_RejectsInsufficientStockWithoutMutation()
    {
        var item = new InventoryItem();
        item.Increase(2);
        Assert.Throws<InvalidOperationException>(() => item.Decrease(3));
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void HasEnough_ReturnsExpectedValues()
    {
        var item = new InventoryItem();
        item.Increase(5);
        Assert.True(item.HasEnough(5));
        Assert.True(item.HasEnough(3));
        Assert.False(item.HasEnough(6));
        Assert.False(item.HasEnough(0));
        Assert.False(item.HasEnough(-1));
    }
}

public class PurchaseTests
{
    [Fact]
    public void AddItem_CalculatesTotalForMultipleItems()
    {
        var purchase = new Purchase();
        purchase.AddItem(new PurchaseItem { Quantity = 2, PurchasePrice = 10m });
        purchase.AddItem(new PurchaseItem { Quantity = 3, PurchasePrice = 5m });
        Assert.Equal(35m, purchase.CalculateTotal());
        Assert.Equal(35m, purchase.TotalAmount);
        Assert.Equal(2, purchase.Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_RejectsInvalidQuantity(int quantity)
    {
        var purchase = new Purchase();
        Assert.Throws<ArgumentException>(() => purchase.AddItem(new PurchaseItem { Quantity = quantity, PurchasePrice = 5 }));
        Assert.Empty(purchase.Items);
    }

    [Fact]
    public void AddItem_RejectsNegativePrice()
    {
        var purchase = new Purchase();
        Assert.Throws<ArgumentException>(() => purchase.AddItem(new PurchaseItem { Quantity = 1, PurchasePrice = -5 }));
    }

    [Fact]
    public void StatusMethods_UpdatePurchaseStatus()
    {
        var purchase = new Purchase();
        Assert.Equal(PurchaseStatus.Created, purchase.Status);
        purchase.MarkAsOrdered();
        Assert.Equal(PurchaseStatus.Ordered, purchase.Status);
        purchase.MarkAsReceived();
        Assert.Equal(PurchaseStatus.Received, purchase.Status);
        purchase.Cancel();
        Assert.Equal(PurchaseStatus.Cancelled, purchase.Status);
    }

    [Fact]
    public void PurchaseItem_GetSubtotal_MultipliesPriceAndQuantity()
    {
        Assert.Equal(37.5m, new PurchaseItem { Quantity = 3, PurchasePrice = 12.5m }.GetSubtotal());
    }
}

public class SaleTests
{
    [Fact]
    public void AddItem_CalculatesTotalForMultipleItems()
    {
        var sale = new Sale();
        sale.AddItem(new SaleItem { Quantity = 2, UnitPrice = 50m });
        sale.AddItem(new SaleItem { Quantity = 1, UnitPrice = 25m });
        Assert.Equal(125m, sale.CalculateTotal());
        Assert.Equal(125m, sale.TotalAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_RejectsInvalidQuantity(int quantity)
    {
        var sale = new Sale();
        Assert.Throws<ArgumentException>(() => sale.AddItem(new SaleItem { Quantity = quantity, UnitPrice = 1 }));
        Assert.Empty(sale.Items);
    }

    [Fact]
    public void AddItem_RejectsNegativeUnitPrice()
    {
        Assert.Throws<ArgumentException>(() => new Sale().AddItem(new SaleItem { Quantity = 1, UnitPrice = -1 }));
    }

    [Fact]
    public void Complete_RejectsEmptySale()
    {
        Assert.Throws<InvalidOperationException>(() => new Sale().Complete());
    }

    [Fact]
    public void Complete_SetsIsCompleted()
    {
        var sale = new Sale { PaymentMethod = PaymentMethod.Card };
        sale.AddItem(new SaleItem { Quantity = 1, UnitPrice = 2 });
        sale.Complete();
        Assert.True(sale.IsCompleted);
        Assert.Equal(PaymentMethod.Card, sale.PaymentMethod);
    }

    [Fact]
    public void SaleItem_GetSubtotal_MultipliesPriceAndQuantity()
    {
        Assert.Equal(15m, new SaleItem { Quantity = 3, UnitPrice = 5m }.GetSubtotal());
    }
}

public class ReceiptTests
{
    [Fact]
    public void Register_SetsFlagAndTimestamp()
    {
        var receipt = new Receipt();
        var before = DateTime.UtcNow;
        receipt.Register();
        var after = DateTime.UtcNow;
        Assert.True(receipt.IsRegistered);
        Assert.InRange(receipt.ReceivedAt, before, after);
    }

    [Fact]
    public void Register_RejectsSecondRegistration()
    {
        var receipt = new Receipt();
        receipt.Register();
        Assert.Throws<InvalidOperationException>(() => receipt.Register());
    }
}
