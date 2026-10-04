namespace Shop.Domain;

/// <summary>Нарушение бизнес-правила; <see cref="Code"/> — код ошибки заглавными буквами.</summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
