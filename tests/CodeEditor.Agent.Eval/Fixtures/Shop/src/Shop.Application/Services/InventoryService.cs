using Shop.Application.Repositories;
using Shop.Domain;

namespace Shop.Application.Services;

/// <summary>Склад: резерв при оформлении заказа и возврат при отмене.</summary>
public sealed class InventoryService(IStockRepository stock)
{
    public int Available(Guid productId) => stock.Get(productId);

    /// <summary>
    /// Всё или ничего: сначала проверяется суммарная потребность по каждому товару (один товар может быть в нескольких
    /// позициях), и только потом склад уменьшается.
    /// </summary>
    public void Reserve(IReadOnlyList<OrderLine> lines)
    {
        var needed = lines.GroupBy(line => line.ProductId).Select(group => (ProductId: group.Key, Quantity: group.Sum(line => line.Quantity))).ToList();
        foreach (var (productId, quantity) in needed)
        {
            if (stock.Get(productId) < quantity)
            {
                throw new DomainException("OUT_OF_STOCK", $"Не хватает товара {productId}: нужно {quantity}, есть {stock.Get(productId)}.");
            }
        }

        foreach (var (productId, quantity) in needed)
        {
            stock.Set(productId, stock.Get(productId) - quantity);
        }
    }

    public void Release(IReadOnlyList<OrderLine> lines)
    {
        foreach (var line in lines)
        {
            stock.Set(line.ProductId, stock.Get(line.ProductId) + line.Quantity);
        }
    }
}
