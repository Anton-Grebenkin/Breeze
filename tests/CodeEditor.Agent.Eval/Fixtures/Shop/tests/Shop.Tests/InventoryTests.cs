using Shop.Domain;

namespace Shop.Tests;

public sealed class InventoryTests
{
    [Fact]
    public void Place_SubtractsStock()
    {
        var app = TestShop.Create();
        var tea = app.AddProduct("Чай", 100m, stock: 10);

        app.Orders.Place(app.CreateOrder((tea, 3)).Id);

        Assert.Equal(7, app.Inventory.Available(tea.Id));
    }

    [Fact]
    public void Place_NotEnoughStock_Throws()
    {
        var app = TestShop.Create();
        var tea = app.AddProduct("Чай", 100m, stock: 2);
        var order = app.CreateOrder((tea, 3));

        var error = Assert.Throws<DomainException>(() => app.Orders.Place(order.Id));

        Assert.Equal("OUT_OF_STOCK", error.Code);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(2, app.Inventory.Available(tea.Id));
    }

    [Fact]
    public void Cancel_PlacedOrder_ReturnsStock()
    {
        var app = TestShop.Create();
        var tea = app.AddProduct("Чай", 100m, stock: 10);
        var order = app.CreateOrder((tea, 4));
        app.Orders.Place(order.Id);

        app.Orders.Cancel(order.Id);

        Assert.Equal(10, app.Inventory.Available(tea.Id));
    }
}
