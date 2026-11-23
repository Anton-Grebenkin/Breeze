using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Reading;

namespace CodeEditor.UI.Tests.Engine;

/// <summary>
/// The browser tool's page scripts on the real engine, without a window or input (ADR 0054): the snapshot reads tables
/// as rows, open shadow roots, frames of the same site and names a canvas; refs work inside a shadow root and a frame;
/// "find" and the Markdown text read the same places.
/// </summary>
public sealed class PageScriptsTests(PageScriptsFixture fixture) : IClassFixture<PageScriptsFixture>
{
    [Fact]
    public async Task Snapshot_ReadsTablesShadowRootsFramesAndGraphics_ButNoPasswordOrHiddenText()
    {
        await fixture.OpenAsync("/");

        var snapshot = await BrowserPageReader.SnapshotAsync(fixture.Tab, part: null, TestContext.Current.CancellationToken);

        Assert.StartsWith($"<browser_page tab=\"t1\" url=\"http://localhost:{fixture.Site.Port}/\" title=\"Стенд\">", snapshot, StringComparison.Ordinal);
        Assert.Contains("- heading \"Приложения\"", snapshot, StringComparison.Ordinal);

        // Text around a link stays, in order.
        Assert.Matches("- text \"Всего\"\n- text \"два\"\n- text \"приложения, см\\.\"\n- link \"описание\" \\[ref=e\\d+\\] -> /docs\n- text \"ниже\\.\"", snapshot);
        Assert.Contains("- table (3 rows)\n  | Имя | Состояние |  |\n  | shop | Synced | button \"Sync\" [ref=e", snapshot, StringComparison.Ordinal);
        Assert.Contains("- text \"Текст в теневом дереве\"", snapshot, StringComparison.Ordinal);
        Assert.Matches("- button \"Теневая кнопка\" \\[ref=e\\d+\\]", snapshot);
        Assert.Contains("- frame \"Журнал\"\n  - text \"Строка журнала во фрейме\"\n  - button \"Кнопка фрейма\" [ref=e", snapshot, StringComparison.Ordinal);
        Assert.Matches("- graphic \"Нагрузка\" \\(canvas, 400x200\\) \\[ref=e\\d+\\]", snapshot);
        Assert.DoesNotContain("secret123", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("скрытый текст", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refs_WorkInsideAShadowRootAndAFrame()
    {
        await fixture.OpenAsync("/");
        var snapshot = await BrowserPageReader.SnapshotAsync(fixture.Tab, part: null, TestContext.Current.CancellationToken);

        var afterShadow = await fixture.UseAsync(new() { ["action"] = "click", ["ref"] = Ref(snapshot, "button \"Теневая кнопка\"") });
        Assert.Contains("title=\"нажато в тени\"", afterShadow, StringComparison.Ordinal);

        var afterFrame = await fixture.UseAsync(new() { ["action"] = "click", ["ref"] = Ref(afterShadow, "button \"Кнопка фрейма\"") });
        Assert.Contains("title=\"нажато во фрейме\"", afterFrame, StringComparison.Ordinal);

        // The card names the element the way the page shows it.
        Assert.Equal("""{"name":"Sync","ok":true,"role":"button"}""", await EvaluateAsync(BrowserScripts.Describe(Ref(snapshot, "| blog | OutOfSync | button \"Sync\""))));
    }

    [Fact]
    public async Task Find_CountsMatchesEverywhere_WithTheHeadingAndContext()
    {
        await fixture.OpenAsync("/");
        await BrowserPageReader.SnapshotAsync(fixture.Tab, part: null, TestContext.Current.CancellationToken);

        var sync = await BrowserPageReader.FindAsync(fixture.Tab, "sync", TestContext.Current.CancellationToken);
        var frame = await BrowserPageReader.FindAsync(fixture.Tab, "ЖУРНАЛА", TestContext.Current.CancellationToken);
        var shadow = await BrowserPageReader.FindAsync(fixture.Tab, "теневом", TestContext.Current.CancellationToken);
        var hidden = await BrowserPageReader.FindAsync(fixture.Tab, "скрытый", TestContext.Current.CancellationToken);

        Assert.StartsWith("«sync»: совпадений 4, мест 4.\n1. под заголовком «Приложения»: Synced\n2. под заголовком «Приложения» [ref=e", sync, StringComparison.Ordinal);
        Assert.Equal("«ЖУРНАЛА»: совпадений 1, мест 1.\n1. под заголовком «Приложения»: Строка журнала во фрейме", frame);
        Assert.Contains("Текст в теневом дереве", shadow, StringComparison.Ordinal);
        Assert.Equal("Текста «скрытый» на странице нет.", hidden);
    }

    [Fact]
    public async Task Read_GivesTheMainTextAsMarkdown_WithoutMenusAndFooter()
    {
        await fixture.OpenAsync("/docs");

        var text = await BrowserPageReader.TextAsync(fixture.Tab, part: null, TestContext.Current.CancellationToken);

        Assert.Equal(
            $"""
            <browser_text tab="t1" url="http://localhost:{fixture.Site.Port}/docs" title="Документация">
            # Установка

            Скачайте [установщик](http://localhost:{fixture.Site.Port}/setup.exe) и **запустите** его.

            - Первый шаг
            - Второй шаг
              - Вложенный

            ```
            dotnet build
            dotnet test
            ```

            | Ключ | Значение |
            | --- | --- |
            | порт | 5000 |
            </browser_text>
            """.ReplaceLineEndings("\n"),
            text);
    }

    // A chart is looked at alone: the image is as large as the element.
    [Fact]
    public async Task Screenshot_OfAnElement_IsItsSize()
    {
        await fixture.OpenAsync("/");
        var snapshot = await BrowserPageReader.SnapshotAsync(fixture.Tab, part: null, TestContext.Current.CancellationToken);

        fixture.Images.Shown.Clear();
        await fixture.UseAsync(new() { ["action"] = "screenshot", ["ref"] = Ref(snapshot, "graphic \"Нагрузка\"") });
        var png = Assert.Single(fixture.Images.Shown);

        // PNG: the signature, then the IHDR chunk with width and height as big-endian integers.
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        Assert.Equal((400, 200), (png[16] << 24 | png[17] << 16 | png[18] << 8 | png[19], png[20] << 24 | png[21] << 16 | png[22] << 8 | png[23]));
    }

    // A real mouse click: the page sees a trusted event, as from a person.
    [Fact]
    public async Task Click_IsTrusted_HoverAndDragMoveTheMouse()
    {
        await fixture.OpenAsync("/");
        var snapshot = await BrowserPageReader.SnapshotAsync(fixture.Tab, part: null, TestContext.Current.CancellationToken);

        var clicked = await fixture.UseAsync(new() { ["action"] = "click", ["ref"] = Ref(snapshot, "| shop | Synced | button \"Sync\"") });
        Assert.Contains("title=\"sync shop true\"", clicked, StringComparison.Ordinal);

        var hovered = await fixture.UseAsync(new() { ["action"] = "hover", ["ref"] = Ref(clicked, "button \"Наведи\"") });
        Assert.Contains("title=\"hover\"", hovered, StringComparison.Ordinal);

        var dragged = await fixture.UseAsync(new() { ["action"] = "drag", ["ref"] = Ref(hovered, "button \"Ручка\""), ["to"] = Ref(hovered, "button \"Гнездо\"") });
        Assert.Contains("title=\"dragged\"", dragged, StringComparison.Ordinal);

        var dropped = await fixture.UseAsync(new() { ["action"] = "drag", ["ref"] = Ref(dragged, "button \"Карточка\""), ["to"] = Ref(dragged, "button \"Корзина\"") });
        Assert.Contains("title=\"dropped card\"", dropped, StringComparison.Ordinal);
    }

    // The file goes into the field without the system dialog, and the page sees it.
    [Fact]
    public async Task Upload_PutsTheFileIntoTheField()
    {
        await fixture.OpenAsync("/");
        var snapshot = await BrowserPageReader.SnapshotAsync(fixture.Tab, part: null, TestContext.Current.CancellationToken);

        var result = await fixture.UseAsync(new() { ["action"] = "upload", ["ref"] = Ref(snapshot, "file \"Файл\""), ["path"] = "orders.csv" });

        Assert.StartsWith("Файл orders.csv выбран в поле.", result, StringComparison.Ordinal);
        Assert.Equal("\"orders.csv\"", await EvaluateAsync("document.getElementById('upload').files[0].name"));
    }

    private Task<string> EvaluateAsync(string script) => fixture.Tab.Page.EvaluateAsync(script, TestContext.Current.CancellationToken);

    /// <summary>The ref of the first snapshot element after <paramref name="marker"/>.</summary>
    private static string Ref(string snapshot, string marker)
    {
        var at = snapshot.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(at >= 0, $"В снимке нет «{marker}»:\n{snapshot}");
        var start = snapshot.IndexOf("[ref=", at, StringComparison.Ordinal) + "[ref=".Length;
        return snapshot[start..snapshot.IndexOf(']', start)];
    }
}
