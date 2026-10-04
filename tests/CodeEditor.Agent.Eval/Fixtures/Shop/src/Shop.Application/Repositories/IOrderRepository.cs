using Shop.Domain;

namespace Shop.Application.Repositories;

public interface IOrderRepository
{
    Order? Find(Guid id);

    void Add(Order order);
}
