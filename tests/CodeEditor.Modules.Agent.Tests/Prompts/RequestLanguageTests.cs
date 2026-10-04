using CodeEditor.Modules.Agent.Services.Prompts;

namespace CodeEditor.Modules.Agent.Tests.Prompts;

/// <summary>The turn reminder names the request language when the request is written in Cyrillic.</summary>
public sealed class RequestLanguageTests
{
    [Theory]
    [InlineData("Добавь в магазин промокоды: `PromoCode`, `IPromoCodeRepository`, `OrderService.ApplyPromoCode`.", "Russian")]
    [InlineData("Add promo codes to the shop.", null)]
    [InlineData("`Parser.Evaluate` → `ArgumentException`", null)]
    [InlineData("", null)]
    public void CyrillicRequest_IsRussian(string request, string? expected) =>
        Assert.Equal(expected, RequestLanguage.Name(request));

    [Fact]
    public void Reminder_NamesTheLanguage_OrIsEmpty()
    {
        Assert.Contains("in Russian", RequestLanguage.Reminder("исправь ошибку"), StringComparison.Ordinal);
        Assert.Empty(RequestLanguage.Reminder("fix the bug"));
    }

    // Native-reasoning models get no reasoning language (they drafted the answer in their reasoning); think-aloud
    // models do, since their reasoning is part of the text.
    [Fact]
    public void ReasoningLanguage_OnlyForThinkingAloud()
    {
        Assert.DoesNotContain("reasoning", RequestLanguage.Reminder("исправь ошибку"), StringComparison.Ordinal);
        Assert.Contains("your reasoning", RequestLanguage.Reminder("исправь ошибку", thinksAloud: true), StringComparison.Ordinal);
    }
}
