using Shop.Domain;

namespace Shop.Application.Repositories;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = [];

    public Order? Find(Guid id) => _orders.GetValueOrDefault(id);

    public void Add(Order order) => _orders[order.Id] = order;
}
