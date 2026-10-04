using Shop.Domain;

namespace Shop.Application.Repositories;

public interface IProductRepository
{
    Product? Find(Guid id);

    void Add(Product product);
}
