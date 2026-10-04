namespace Shop.Tests;

public sealed class NotificationTests
{
    [Fact]
    public void Place_SendsEmailWithTotal()
    {
        var app = TestShop.Create();
        var order = app.CreateOrder((app.AddProduct("Чай", 100m), 1));

        app.Orders.Place(order.Id);

        var email = Assert.Single(app.Emails.Sent);
        Assert.Equal("client@example.com", email.To);
        Assert.Contains("Сумма к оплате: 120.00 ₽", email.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_SendsEmail()
    {
        var app = TestShop.Create();
        var order = app.CreateOrder((app.AddProduct("Чай", 100m), 1));
        app.Orders.Place(order.Id);

        app.Orders.Cancel(order.Id);

        Assert.Equal(2, app.Emails.Sent.Count);
        Assert.Equal("Заказ отменён", app.Emails.Sent[^1].Subject);
    }
}
