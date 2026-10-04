using Shop.Domain;

namespace Shop.Application.Services;

public sealed class InvoiceService(PricingService pricing)
{
    public Invoice Build(Order order) =>
        new(order.Id, order.Lines, pricing.Subtotal(order), pricing.Tax(order), pricing.Total(order));
}
