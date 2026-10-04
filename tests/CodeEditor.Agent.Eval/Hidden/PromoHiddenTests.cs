using Shop.Application;
using Shop.Domain;

namespace Shop.Tests;

public sealed class PromoHiddenTests
{
    private static readonly DateTimeOffset Today = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NextWeek = Today.AddDays(7);

    private sealed class Clock : IClock
    {
        public DateTimeOffset Now { get; set; } = Today;
    }

    private readonly Clock _clock = new();
    private readonly ShopApp _app;
    private readonly Product _tea;

    public PromoHiddenTests()
    {
        _app = ShopApp.Create(_clock);
        _tea = new Product(Guid.NewGuid(), "Чай", new Money(100m));
        _app.Products.Add(_tea);
        _app.Stock.Set(_tea.Id, 100);
        _app.Promos.Add(new PromoCode("SALE10", PromoKind.Percent, 10m, NextWeek, 5));
        _app.Promos.Add(new PromoCode("MINUS50", PromoKind.Fixed, 50m, NextWeek, 5));
        _app.Promos.Add(new PromoCode("ONCE", PromoKind.Percent, 10m, NextWeek, 1));
    }

    [Fact]
    public void NoPromo_NoDiscount()
    {
        var invoice = _app.Invoices.Build(CreateOrder(2));

        Assert.Equal(Money.Zero, invoice.Discount);
        Assert.Equal(new Money(240m), invoice.Total);
    }

    [Fact]
    public void Percent_AppliedBeforeTax()
    {
        var order = CreateOrder(2);

        _app.Orders.ApplyPromoCode(order.Id, "SALE10");

        AssertSums(order, discount: 20m, tax: 36m, total: 216m);
    }

    [Fact]
    public void Fixed_AppliedBeforeTax()
    {
        var order = CreateOrder(2);

        _app.Orders.ApplyPromoCode(order.Id, "MINUS50");

        AssertSums(order, discount: 50m, tax: 30m, total: 180m);
    }

    [Fact]
    public void Fixed_LargerThanSubtotal_TotalIsZero()
    {
        var order = CreateOrder(1, new Product(Guid.NewGuid(), "Спички", new Money(30m)));

        _app.Orders.ApplyPromoCode(order.Id, "MINUS50");

        AssertSums(order, discount: 30m, tax: 0m, total: 0m);
    }

    [Fact]
    public void Percent_RoundedToKopecks()
    {
        var order = CreateOrder(1, new Product(Guid.NewGuid(), "Спички", new Money(0.99m)));

        _app.Orders.ApplyPromoCode(order.Id, "SALE10");

        AssertSums(order, discount: 0.10m, tax: 0.18m, total: 1.07m);
    }

    [Fact]
    public void Code_IgnoresCaseAndSpaces()
    {
        var order = CreateOrder(2);

        _app.Orders.ApplyPromoCode(order.Id, "  sale10 ");

        Assert.Equal(new Money(216m), _app.Pricing.Total(order));
    }

    [Fact]
    public void UnknownCode_Throws() => AssertCode("PROMO_NOT_FOUND", () => _app.Orders.ApplyPromoCode(CreateOrder(1).Id, "NOPE"));

    [Fact]
    public void ExpiredCode_Throws()
    {
        var order = CreateOrder(1);
        _clock.Now = NextWeek.AddSeconds(1);

        AssertCode("PROMO_EXPIRED", () => _app.Orders.ApplyPromoCode(order.Id, "SALE10"));
    }

    [Fact]
    public void Code_ValidAtExpiryMoment()
    {
        var order = CreateOrder(2);
        _clock.Now = NextWeek;

        _app.Orders.ApplyPromoCode(order.Id, "SALE10");

        Assert.Equal(new Money(216m), _app.Pricing.Total(order));
    }

    [Fact]
    public void SecondCode_Throws()
    {
        var order = CreateOrder(2);
        _app.Orders.ApplyPromoCode(order.Id, "SALE10");

        AssertCode("PROMO_ALREADY_APPLIED", () => _app.Orders.ApplyPromoCode(order.Id, "MINUS50"));
        AssertCode("PROMO_ALREADY_APPLIED", () => _app.Orders.ApplyPromoCode(order.Id, "SALE10"));
        Assert.Equal(new Money(216m), _app.Pricing.Total(order));
    }

    [Fact]
    public void PlacedOrder_Throws()
    {
        var order = CreateOrder(1);
        _app.Orders.Place(order.Id);

        AssertCode("ORDER_NOT_DRAFT", () => _app.Orders.ApplyPromoCode(order.Id, "SALE10"));
    }

    [Fact]
    public void Use_CountedOnPlaceOnly()
    {
        var order = CreateOrder(1);

        _app.Orders.ApplyPromoCode(order.Id, "SALE10");
        Assert.Equal(0, Used("SALE10"));

        _app.Orders.Place(order.Id);
        Assert.Equal(1, Used("SALE10"));
    }

    [Fact]
    public void ExhaustedCode_ApplyThrows()
    {
        PlaceWith("ONCE");

        AssertCode("PROMO_EXHAUSTED", () => _app.Orders.ApplyPromoCode(CreateOrder(1).Id, "ONCE"));
    }

    [Fact]
    public void ExhaustedAtPlace_OrderStaysDraftAndStockKept()
    {
        var first = CreateOrder(1);
        var second = CreateOrder(3);
        _app.Orders.ApplyPromoCode(first.Id, "ONCE");
        _app.Orders.ApplyPromoCode(second.Id, "ONCE");
        _app.Orders.Place(first.Id);

        AssertCode("PROMO_EXHAUSTED", () => _app.Orders.Place(second.Id));

        Assert.Equal(OrderStatus.Draft, second.Status);
        Assert.Equal(99, _app.Stock.Get(_tea.Id));
        Assert.Equal(1, Used("ONCE"));
    }

    [Fact]
    public void ExpiredAtPlace_OrderStaysDraftAndStockKept()
    {
        var order = CreateOrder(3);
        _app.Orders.ApplyPromoCode(order.Id, "SALE10");
        _clock.Now = NextWeek.AddDays(1);

        AssertCode("PROMO_EXPIRED", () => _app.Orders.Place(order.Id));

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(100, _app.Stock.Get(_tea.Id));
        Assert.Equal(0, Used("SALE10"));
    }

    [Fact]
    public void CancelPlaced_ReturnsUseOnce()
    {
        var order = PlaceWith("ONCE");

        _app.Orders.Cancel(order.Id);
        CancelIfAllowed(order.Id);

        Assert.Equal(0, Used("ONCE"));
        _app.Orders.ApplyPromoCode(CreateOrder(1).Id, "ONCE");
    }

    [Fact]
    public void CancelDraft_KeepsUses()
    {
        PlaceWith("SALE10");
        var draft = CreateOrder(1);
        _app.Orders.ApplyPromoCode(draft.Id, "SALE10");

        CancelIfAllowed(draft.Id);

        Assert.Equal(1, Used("SALE10"));
    }

    [Fact]
    public void Email_ShowsDiscountedTotal()
    {
        PlaceWith("SALE10", quantity: 2);

        var email = Assert.Single(_app.Emails.Sent);
        Assert.Contains("Сумма к оплате: 216.00 ₽", email.Body, StringComparison.Ordinal);
    }

    private Order CreateOrder(int quantity, Product? product = null)
    {
        if (product is not null)
        {
            _app.Products.Add(product);
            _app.Stock.Set(product.Id, 100);
        }

        var order = _app.Orders.Create("client@example.com");
        _app.Orders.AddItem(order.Id, (product ?? _tea).Id, quantity);
        return order;
    }

    private Order PlaceWith(string code, int quantity = 1)
    {
        var order = CreateOrder(quantity);
        _app.Orders.ApplyPromoCode(order.Id, code);
        _app.Orders.Place(order.Id);
        return order;
    }

    private int Used(string code) => _app.Promos.Find(code)!.Used;

    // Можно ли отменять черновик и отменять повторно, задача не говорит: запрет через DomainException — тоже верное
    // решение, лишь бы счётчик сходился.
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

    private void AssertSums(Order order, decimal discount, decimal tax, decimal total)
    {
        var invoice = _app.Invoices.Build(order);
        Assert.Equal(new Money(discount), invoice.Discount);
        Assert.Equal(new Money(tax), invoice.Tax);
        Assert.Equal(new Money(total), invoice.Total);
        Assert.Equal(new Money(total), _app.Pricing.Total(order));
    }

    private static void AssertCode(string code, Action action) => Assert.Equal(code, Assert.Throws<DomainException>(action).Code);
}
