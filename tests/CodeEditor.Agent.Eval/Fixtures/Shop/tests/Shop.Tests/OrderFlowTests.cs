using Shop.Domain;

namespace Shop.Tests;

public sealed class OrderFlowTests
{
    [Fact]
    public void Place_EmptyOrder_Throws()
    {
        var app = TestShop.Create();
        var order = app.Orders.Create("client@example.com");

        var error = Assert.Throws<DomainException>(() => app.Orders.Place(order.Id));

        Assert.Equal("EMPTY_ORDER", error.Code);
    }

    [Fact]
    public void AddItem_UnknownProduct_Throws()
    {
        var app = TestShop.Create();
        var order = app.Orders.Create("client@example.com");

        var error = Assert.Throws<DomainException>(() => app.Orders.AddItem(order.Id, Guid.NewGuid(), 1));

        Assert.Equal("PRODUCT_NOT_FOUND", error.Code);
    }

    [Fact]
    public void AddItem_ToPlacedOrder_Throws()
    {
        var app = TestShop.Create();
        var tea = app.AddProduct("Чай", 100m);
        var order = app.CreateOrder((tea, 1));
        app.Orders.Place(order.Id);

        var error = Assert.Throws<DomainException>(() => app.Orders.AddItem(order.Id, tea.Id, 1));

        Assert.Equal("ORDER_NOT_DRAFT", error.Code);
    }

    [Fact]
    public void Place_ChangesStatus()
    {
        var app = TestShop.Create();
        var order = app.CreateOrder((app.AddProduct("Чай", 100m), 1));

        app.Orders.Place(order.Id);

        Assert.Equal(OrderStatus.Placed, order.Status);
    }

    [Fact]
    public void Cancel_ChangesStatus()
    {
        var app = TestShop.Create();
        var order = app.CreateOrder((app.AddProduct("Чай", 100m), 1));
        app.Orders.Place(order.Id);

        app.Orders.Cancel(order.Id);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }
}
