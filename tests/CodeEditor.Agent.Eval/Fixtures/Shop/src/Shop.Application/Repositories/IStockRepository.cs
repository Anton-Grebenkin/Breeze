namespace Shop.Application.Repositories;

/// <summary>Остатки на складе: сколько штук товара можно продать.</summary>
public interface IStockRepository
{
    int Get(Guid productId);

    void Set(Guid productId, int quantity);
}
