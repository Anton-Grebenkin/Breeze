namespace CodeEditor.Agent.Eval;

/// <summary>
/// Hard tasks on the Shop repository (three projects, rules in AGENTS.md): a spec-driven feature with edge cases (promo
/// codes), a bug from its symptom (a kopeck off in an email) and two related stock bugs. Scored by the share of hidden
/// tests passed, which shows how close the agent got even without a full solution.
/// </summary>
internal static class ShopTasks
{
    public const string Fixture = "Shop";

    private const string Notifications = "src/Shop.Application/Services/NotificationService.cs";
    private const string Inventory = "src/Shop.Application/Services/InventoryService.cs";
    private const string Orders = "src/Shop.Application/Services/OrderService.cs";
    private const string FloatingMoney = @"\bdouble\b|\bfloat\b";

    public static IReadOnlyList<EvalTask> All() =>
    [
        new("shop-promo", PromoPrompt)
        {
            Fixture = Fixture,
            HiddenTests = "PromoHiddenTests.cs",
            Forbidden = FloatingMoney + @"|DateTime(Offset)?\.(Now|UtcNow|Today)\b",
        },
        new("shop-penny", "Клиенты жалуются: сумма в письме «Заказ оформлен» иногда на копейку отличается от суммы в счёте. Найди причину и исправь; добавь тест, который ловит эту ошибку.")
        {
            Fixture = Fixture,
            Seeds = [(Notifications, "using Shop.Domain;", "using System.Globalization;\nusing Shop.Domain;"), (Notifications, PennyCorrect, PennySeeded)],
            HiddenTests = "PennyHiddenTests.cs",
            Forbidden = FloatingMoney,
        },
        new("shop-stock", "Остатки на складе расходятся с реальностью: после неудачной попытки оформить заказ часть товара пропадает со склада, а после отмены заказа товара иногда становится больше, чем было. Найди все причины, исправь и добавь тесты.")
        {
            Fixture = Fixture,
            Seeds = [(Inventory, ReserveCorrect, ReserveSeeded), (Orders, CancelCorrect, CancelSeeded)],
            HiddenTests = "StockHiddenTests.cs",
        },
    ];

    private const string PromoPrompt = """
        Добавь в магазин промокоды.

        API (тесты будут обращаться именно к нему):
        - в Shop.Domain: `enum PromoKind { Percent, Fixed }` и `PromoCode` — конструктор `(string code, PromoKind kind, decimal value, DateTimeOffset expiresAt, int maxUses)`, свойства `Code`, `Kind`, `Value`, `ExpiresAt`, `MaxUses` и `Used` (сколько раз код уже использован);
        - в Shop.Application.Repositories: `IPromoCodeRepository` с методами `PromoCode? Find(string code)` и `void Add(PromoCode promo)`, реализация в памяти `InMemoryPromoCodeRepository`; в `ShopApp` — свойство `Promos`;
        - `OrderService.ApplyPromoCode(Guid orderId, string code)`;
        - в `Invoice` — свойство `Discount` (Money).

        Правила:
        1. Код ищется без учёта регистра и пробелов по краям.
        2. Скидка считается от суммы позиций до НДС: Percent — процент (Value = 10 — это 10 %), Fixed — рубли. Скидка округляется до копеек и не больше суммы позиций. НДС начисляется на сумму после скидки. `PricingService`, счёт и письмо показывают суммы со скидкой.
        3. Применить код можно только к черновику (иначе ORDER_NOT_DRAFT) и только один раз: второй код, даже тот же самый, — PROMO_ALREADY_APPLIED.
        4. Неизвестный код — PROMO_NOT_FOUND. Код действует до `ExpiresAt` включительно, позже — PROMO_EXPIRED. Время — через `IClock`.
        5. Использование засчитывается при оформлении заказа (`Place`), а не при применении. Если `Used` уже равно `MaxUses` — PROMO_EXHAUSTED.
        6. Срок и лимит проверяются и при применении, и при оформлении. Если оформить нельзя, заказ остаётся черновиком, склад и счётчик использований не меняются.
        7. Отмена оформленного заказа возвращает использование кода; отмена черновика и повторная отмена счётчик не трогают.

        Добавь тесты.
        """;

    private const string PennyCorrect = """
            public void OrderPlaced(Order order) =>
                email.Send(order.CustomerEmail, "Заказ оформлен", $"Ваш заказ {order.Id} оформлен. Сумма к оплате: {pricing.Total(order)} ₽.");
        """;

    private const string PennySeeded = """
            public void OrderPlaced(Order order)
            {
                // Итог в письме — как в кассовом чеке: каждая позиция с НДС, округлённая до копеек.
                double total = 0;
                foreach (var line in order.Lines)
                {
                    total += Math.Round((double)line.Total.Amount * (1 + (double)PricingService.VatRate), 2);
                }

                email.Send(order.CustomerEmail, "Заказ оформлен", $"Ваш заказ {order.Id} оформлен. Сумма к оплате: {total.ToString("0.00", CultureInfo.InvariantCulture)} ₽.");
            }
        """;

    private const string ReserveCorrect = """
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
        """;

    private const string ReserveSeeded = """
            public void Reserve(IReadOnlyList<OrderLine> lines)
            {
                foreach (var line in lines)
                {
                    var available = stock.Get(line.ProductId);
                    if (available < line.Quantity)
                    {
                        throw new DomainException("OUT_OF_STOCK", $"Не хватает товара {line.ProductName}: нужно {line.Quantity}, есть {available}.");
                    }

                    stock.Set(line.ProductId, available - line.Quantity);
                }
            }
        """;

    private const string CancelCorrect = """
            /// <summary>Отмена: оформленный заказ возвращает товар на склад, черновик склад не трогает, повтор — ничего не делает.</summary>
            public void Cancel(Guid orderId)
            {
                var order = Get(orderId);
                if (order.Status == OrderStatus.Cancelled)
                {
                    return;
                }

                if (order.Status == OrderStatus.Placed)
                {
                    inventory.Release(order.Lines);
                }

                order.MarkCancelled();
        """;

    private const string CancelSeeded = """
            public void Cancel(Guid orderId)
            {
                var order = Get(orderId);
                inventory.Release(order.Lines);
                order.MarkCancelled();
        """;
}
