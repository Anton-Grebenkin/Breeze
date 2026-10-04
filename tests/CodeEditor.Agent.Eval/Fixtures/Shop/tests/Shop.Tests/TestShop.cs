using Shop.Application;
using Shop.Domain;

namespace Shop.Tests;

/// <summary>Магазин для тестов: фиксированные часы и товары с остатком на складе.</summary>
public static class TestShop
{
    public static readonly DateTimeOffset Today = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public static ShopApp Create() => ShopApp.Create(new FixedClock(Today));

    public static Product AddProduct(this ShopApp app, string name, decimal price, int stock = 100)
    {
        var product = new Product(Guid.NewGuid(), name, new Money(price));
        app.Products.Add(product);
        app.Stock.Set(product.Id, stock);
        return product;
    }

    public static Order CreateOrder(this ShopApp app, params (Product Product, int Quantity)[] items)
    {
        var order = app.Orders.Create("client@example.com");
        foreach (var (product, quantity) in items)
        {
            app.Orders.AddItem(order.Id, product.Id, quantity);
        }

        return order;
    }
}
