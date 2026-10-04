using Shop.Application.Repositories;
using Shop.Application.Services;
using Shop.Domain;

namespace Shop.Application;

/// <summary>Сборка магазина: хранилища в памяти и сервисы. Через него тесты получают всё нужное.</summary>
public sealed class ShopApp
{
    private ShopApp(IClock clock)
    {
        Clock = clock;
        Pricing = new PricingService();
        Invoices = new InvoiceService(Pricing);
        Notifications = new NotificationService(Emails, Pricing);
        Inventory = new InventoryService(Stock);
        Orders = new OrderService(OrderStore, Products, Inventory, Notifications);
    }

    public IClock Clock { get; }

    public InMemoryProductRepository Products { get; } = new();

    public InMemoryStockRepository Stock { get; } = new();

    public InMemoryOrderRepository OrderStore { get; } = new();

    public InMemoryEmailSender Emails { get; } = new();

    public PricingService Pricing { get; }

    public InvoiceService Invoices { get; }

    public NotificationService Notifications { get; }

    public InventoryService Inventory { get; }

    public OrderService Orders { get; }

    public static ShopApp Create(IClock clock) => new(clock);
}
