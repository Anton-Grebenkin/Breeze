using Shop.Domain;

namespace Shop.Tests;

public sealed class PricingTests
{
    [Fact]
    public void Subtotal_SumsLines()
    {
        var app = TestShop.Create();
        var order = app.CreateOrder((app.AddProduct("Чай", 100m), 2), (app.AddProduct("Сахар", 50m), 1));

        Assert.Equal(new Money(250m), app.Pricing.Subtotal(order));
    }

    [Fact]
    public void Tax_IsTwentyPercent_RoundedToKopecks()
    {
        var app = TestShop.Create();
        var order = app.CreateOrder((app.AddProduct("Спички", 0.99m), 1));

        Assert.Equal(new Money(0.20m), app.Pricing.Tax(order));
        Assert.Equal(new Money(1.19m), app.Pricing.Total(order));
    }

    [Fact]
    public void Invoice_TakesSumsFromPricing()
    {
        var app = TestShop.Create();
        var order = app.CreateOrder((app.AddProduct("Чай", 100m), 3));

        var invoice = app.Invoices.Build(order);

        Assert.Equal(new Money(300m), invoice.Subtotal);
        Assert.Equal(new Money(60m), invoice.Tax);
        Assert.Equal(new Money(360m), invoice.Total);
    }
}
