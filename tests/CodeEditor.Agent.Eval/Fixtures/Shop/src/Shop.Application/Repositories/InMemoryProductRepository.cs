using Shop.Domain;

namespace Shop.Application.Repositories;

public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly Dictionary<Guid, Product> _products = [];

    public Product? Find(Guid id) => _products.GetValueOrDefault(id);

    public void Add(Product product) => _products[product.Id] = product;
}
