using Shop.Domain;

namespace Shop.Application.Services;

/// <summary>Письма клиенту о заказе.</summary>
public sealed class NotificationService(IEmailSender email, PricingService pricing)
{
    public void OrderPlaced(Order order) =>
        email.Send(order.CustomerEmail, "Заказ оформлен", $"Ваш заказ {order.Id} оформлен. Сумма к оплате: {pricing.Total(order)} ₽.");

    public void OrderCancelled(Order order) =>
        email.Send(order.CustomerEmail, "Заказ отменён", $"Ваш заказ {order.Id} на сумму {pricing.Total(order)} ₽ отменён.");
}
