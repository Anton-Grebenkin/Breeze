namespace Shop.Application.Repositories;

public sealed class InMemoryStockRepository : IStockRepository
{
    private readonly Dictionary<Guid, int> _stock = [];

    public int Get(Guid productId) => _stock.GetValueOrDefault(productId);

    public void Set(Guid productId, int quantity) => _stock[productId] = quantity;
}
