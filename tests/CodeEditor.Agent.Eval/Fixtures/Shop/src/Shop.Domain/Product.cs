namespace Shop.Domain;

/// <summary>Товар каталога с ценой за штуку без НДС.</summary>
public sealed record Product(Guid Id, string Name, Money Price);
