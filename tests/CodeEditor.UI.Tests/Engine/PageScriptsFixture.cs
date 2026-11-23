using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Tabs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using CodeEditor.UI.Tests.Infrastructure;

namespace CodeEditor.UI.Tests.Engine;

/// <summary>
/// A test site, one browser tab on a headless real engine (<see cref="HeadlessBrowserPage"/>) and the agent's
/// <c>browser</c> tool over it, shared by the tests of a class: the site has a table, a web component with a shadow
/// root, a frame, a canvas and an article.
/// </summary>
public sealed class PageScriptsFixture : IDisposable
{
    private const string Home = """
        <html><head><title>Стенд</title></head><body>
        <nav><a href="/docs">Документация</a></nav>
        <h1>Приложения</h1>
        <p>Всего <b>два</b> приложения, см. <a href="/docs">описание</a> ниже.</p>
        <table id="apps">
          <tr><th>Имя</th><th>Состояние</th><th></th></tr>
          <tr><td>shop</td><td>Synced</td><td><button onclick="document.title = 'sync shop ' + event.isTrusted">Sync</button></td></tr>
          <tr><td>blog</td><td>OutOfSync</td><td><button onclick="document.title = 'sync blog'">Sync</button></td></tr>
        </table>
        <my-card></my-card>
        <iframe src="/frame" title="Журнал" width="400" height="120"></iframe>
        <canvas id="chart" width="400" height="200" aria-label="Нагрузка"></canvas>
        <input type="password" value="secret123">
        <input type="text" id="name" aria-label="Имя">
        <input type="file" id="upload" aria-label="Файл">
        <div style="display:none">скрытый текст</div>
        <button id="zone" onmouseenter="document.title = 'hover'">Наведи</button>
        <button id="grip" onmousedown="window.held = true">Ручка</button>
        <button id="slot" style="margin-left:200px" onmouseup="if (window.held) document.title = 'dragged'">Гнездо</button>
        <button id="card" draggable="true" ondragstart="event.dataTransfer.setData('text/plain', 'card')">Карточка</button>
        <button id="bin" ondragover="event.preventDefault()" ondrop="document.title = 'dropped ' + event.dataTransfer.getData('text/plain')">Корзина</button>
        <script>
          class MyCard extends HTMLElement {
            constructor() {
              super();
              const root = this.attachShadow({ mode: 'open' });
              root.innerHTML = '<p>Текст в теневом дереве</p><button id="inner">Теневая кнопка</button>';
              root.getElementById('inner').addEventListener('click', () => document.title = 'нажато в тени');
            }
          }
          customElements.define('my-card', MyCard);
        </script>
        </body></html>
        """;

    private const string Frame = """
        <html><body><p>Строка журнала во фрейме</p><button onclick="parent.document.title = 'нажато во фрейме'">Кнопка фрейма</button></body></html>
        """;

    private const string Docs = """
        <html><head><title>Документация</title></head><body>
        <nav><a href="/">Главная</a></nav>
        <main>
          <h1>Установка</h1>
          <p>Скачайте <a href="/setup.exe">установщик</a> и <strong>запустите</strong> его.</p>
          <ul><li>Первый шаг</li><li>Второй шаг<ul><li>Вложенный</li></ul></li></ul>
          <pre>dotnet build
        dotnet test</pre>
          <table><tr><th>Ключ</th><th>Значение</th></tr><tr><td>порт</td><td>5000</td></tr></table>
        </main>
        <footer>Подвал сайта</footer>
        </body></html>
        """;

    private readonly HeadlessEngine _engine = new();
    private readonly AIFunction _tool;

    public PageScriptsFixture()
    {
        Site = LocalSite.Start(new Dictionary<string, string> { ["/"] = Home, ["/frame"] = Frame, ["/docs"] = Docs, ["/api/items"] = """{"items":[1,2,3]}""" });
        Tabs = new BrowserTabs(_engine);
        var agent = new BrowserAgentTabs(Tabs);
        Tab = agent.Open();
        Folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        File.WriteAllText(Path.Combine(Folder, "orders.csv"), "id;sum");
        var files = new PhysicalFileSystem();
        var workspace = new Workspace(files, new ContextKeyService(), NullLogger<Workspace>.Instance);
        workspace.Open(Folder);
        _tool = new BrowserAgentTools(agent, new PassThroughOutputStore(), Images, workspace, files).CreateTools().OfType<AIFunction>().Single();
    }

    public LocalSite Site { get; }

    /// <summary>The folder the tool works in; it has <c>orders.csv</c>.</summary>
    public string Folder { get; }

    public BrowserTabs Tabs { get; }

    public BrowserTab Tab { get; }

    internal HeadlessBrowserPage Page => (HeadlessBrowserPage)Tab.Page;

    /// <summary>Images the tool showed to the model.</summary>
    internal ShownImages Images { get; } = new();

    /// <summary>Calls the <c>browser</c> tool as the model would.</summary>
    public async Task<string> UseAsync(Dictionary<string, object?> arguments) =>
        (await _tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;

    public Task OpenAsync(string path) => Tab.Page.NavigateAsync(new Uri($"http://localhost:{Site.Port}{path}"), TestContext.Current.CancellationToken);

    public void Dispose()
    {
        Tabs.Dispose();
        Site.Dispose();
        AppSession.DeleteQuietly(Path.GetDirectoryName(Folder)!);
    }

    internal sealed class ShownImages : IAgentImages
    {
        public bool CanShow => true;

        public List<byte[]> Shown { get; } = [];

        public bool TryShow(string name, byte[] data, string mediaType)
        {
            Shown.Add(data);
            return true;
        }
    }

    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }

    private sealed class HeadlessEngine : IBrowserEngine
    {
        public IBrowserPage CreatePage(BrowserPageState? restored = null, bool forAgent = false) => new HeadlessBrowserPage();
    }
}
