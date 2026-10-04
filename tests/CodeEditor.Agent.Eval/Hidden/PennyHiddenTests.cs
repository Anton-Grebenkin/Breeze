using Shop.Application;
using Shop.Domain;

namespace Shop.Tests;

public sealed class PennyHiddenTests
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset Now { get; } = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void TwoLines_EmailTotalMatchesInvoice() => AssertEmailMatchesInvoice([1.13m, 1.13m], expected: "2.71");

    [Fact]
    public void ThreeLines_EmailTotalMatchesInvoice() => AssertEmailMatchesInvoice([0.33m, 0.33m, 0.33m], expected: "1.19");

    [Fact]
    public void SingleLine_EmailTotalMatchesInvoice() => AssertEmailMatchesInvoice([0.99m], expected: "1.19");

    [Fact]
    public void ManyRandomOrders_EmailTotalMatchesInvoice()
    {
        var random = new Random(20260301);
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var prices = Enumerable.Range(0, random.Next(1, 6)).Select(_ => random.Next(1, 100_000) / 100m).ToArray();
            var (email, invoice) = PlaceOrder(prices);
            Assert.Contains($"Сумма к оплате: {invoice.Total} ₽", email, StringComparison.Ordinal);
        }
    }

    private static void AssertEmailMatchesInvoice(decimal[] prices, string expected)
    {
        var (email, invoice) = PlaceOrder(prices);

        Assert.Equal(expected, invoice.Total.ToString());
        Assert.Contains($"Сумма к оплате: {expected} ₽", email, StringComparison.Ordinal);
    }

    private static (string Email, Shop.Application.Services.Invoice Invoice) PlaceOrder(decimal[] prices)
    {
        var app = ShopApp.Create(new Clock());
        var order = app.Orders.Create("client@example.com");
        foreach (var price in prices)
        {
            var product = new Product(Guid.NewGuid(), "Товар", new Money(price));
            app.Products.Add(product);
            app.Stock.Set(product.Id, 10);
            app.Orders.AddItem(order.Id, product.Id, 1);
        }

        app.Orders.Place(order.Id);
        return (app.Emails.Sent.Single(email => email.Subject == "Заказ оформлен").Body, app.Invoices.Build(order));
    }
}
