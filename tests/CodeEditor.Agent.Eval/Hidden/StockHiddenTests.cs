using Shop.Application;
using Shop.Domain;

namespace Shop.Tests;

public sealed class StockHiddenTests
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset Now { get; } = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    }

    private readonly ShopApp _app = ShopApp.Create(new Clock());

    [Fact]
    public void PartialShortage_ReservesNothing()
    {
        var tea = AddProduct(stock: 10);
        var sugar = AddProduct(stock: 1);
        var order = CreateOrder((tea, 2), (sugar, 5));

        var error = Assert.Throws<DomainException>(() => _app.Orders.Place(order.Id));

        Assert.Equal("OUT_OF_STOCK", error.Code);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(10, _app.Stock.Get(tea.Id));
        Assert.Equal(1, _app.Stock.Get(sugar.Id));
    }

    [Fact]
    public void SameProductInTwoLines_NotEnoughInTotal_ReservesNothing()
    {
        var tea = AddProduct(stock: 5);
        var order = CreateOrder((tea, 3), (tea, 3));

        var error = Assert.Throws<DomainException>(() => _app.Orders.Place(order.Id));

        Assert.Equal("OUT_OF_STOCK", error.Code);
        Assert.Equal(5, _app.Stock.Get(tea.Id));
    }

    [Fact]
    public void SameProductInTwoLines_EnoughInTotal_Places()
    {
        var tea = AddProduct(stock: 6);
        var order = CreateOrder((tea, 3), (tea, 3));

        _app.Orders.Place(order.Id);

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(0, _app.Stock.Get(tea.Id));
    }

    [Fact]
    public void FailedPlace_CanBePlacedAfterRestock()
    {
        var tea = AddProduct(stock: 10);
        var sugar = AddProduct(stock: 1);
        var order = CreateOrder((tea, 2), (sugar, 5));
        Assert.Throws<DomainException>(() => _app.Orders.Place(order.Id));

        _app.Stock.Set(sugar.Id, 5);
        _app.Orders.Place(order.Id);

        Assert.Equal(8, _app.Stock.Get(tea.Id));
        Assert.Equal(0, _app.Stock.Get(sugar.Id));
    }

    [Fact]
    public void CancelPlaced_ReturnsStock()
    {
        var tea = AddProduct(stock: 10);
        var order = CreateOrder((tea, 4), (tea, 1));
        _app.Orders.Place(order.Id);

        _app.Orders.Cancel(order.Id);

        Assert.Equal(10, _app.Stock.Get(tea.Id));
    }

    [Fact]
    public void CancelTwice_ReturnsStockOnce()
    {
        var tea = AddProduct(stock: 10);
        var order = CreateOrder((tea, 4));
        _app.Orders.Place(order.Id);

        _app.Orders.Cancel(order.Id);
        CancelIfAllowed(order.Id);

        Assert.Equal(10, _app.Stock.Get(tea.Id));
    }

    [Fact]
    public void CancelDraft_KeepsStock()
    {
        var tea = AddProduct(stock: 10);
        var order = CreateOrder((tea, 4));

        CancelIfAllowed(order.Id);

        Assert.Equal(10, _app.Stock.Get(tea.Id));
    }

    [Fact]
    public void CancelAfterFailedPlace_KeepsStock()
    {
        var tea = AddProduct(stock: 10);
        var sugar = AddProduct(stock: 1);
        var order = CreateOrder((tea, 2), (sugar, 5));
        Assert.Throws<DomainException>(() => _app.Orders.Place(order.Id));

        CancelIfAllowed(order.Id);

        Assert.Equal(10, _app.Stock.Get(tea.Id));
        Assert.Equal(1, _app.Stock.Get(sugar.Id));
    }

    // Можно ли отменять черновик и отменять повторно, задача не говорит: запрет через DomainException — тоже верное
    // решение, лишь бы склад сходился.
    private void CancelIfAllowed(Guid orderId)
    {
        try
        {
            _app.Orders.Cancel(orderId);
        }
        catch (DomainException)
        {
        }
    }

    private Product AddProduct(int stock)
    {
        var product = new Product(Guid.NewGuid(), "Товар", new Money(100m));
        _app.Products.Add(product);
        _app.Stock.Set(product.Id, stock);
        return product;
    }

    private Order CreateOrder(params (Product Product, int Quantity)[] items)
    {
        var order = _app.Orders.Create("client@example.com");
        foreach (var (product, quantity) in items)
        {
            _app.Orders.AddItem(order.Id, product.Id, quantity);
        }

        return order;
    }
}
