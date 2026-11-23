using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CodeEditor.Core.Storage;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Wpf.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// The product's own browser engine (<see cref="WebViewBrowserEngine"/>) in a window far off the screen, on its own UI
/// thread: tabs, dialogs, windows opened by pages and restored pages are tested on real WebView2 pages with no mouse
/// or keyboard. The window hosts the control of every page, as the Browser panel does.
/// </summary>
internal sealed class OffScreenBrowser : IDisposable
{
    private const double OffScreen = -20_000;

    private readonly Dispatcher _dispatcher;
    private readonly string _data = Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", "engine-" + Guid.NewGuid().ToString("N"));
    private Window? _window;
    private Grid? _pages;

    public OffScreenBrowser()
    {
        var started = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            started.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        _dispatcher = started.Task.GetAwaiter().GetResult();
        Directory.CreateDirectory(_data);
        Engine = new WebViewBrowserEngine(new UiDispatcher(_dispatcher), () => { }, new UserDataPaths(_data), () => new BrowserOptions(), NullLogger<WebViewBrowserEngine>.Instance);
        Engine.PagesChanged += (_, _) => _dispatcher.BeginInvoke(HostPages);
        _dispatcher.Invoke(() =>
        {
            _pages = new Grid();
            _window = new Window
            {
                Left = OffScreen, Top = OffScreen, Width = 1200, Height = 900,
                WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Content = _pages,
            };
            _window.Show();
        });
    }

    public WebViewBrowserEngine Engine { get; }

    public void Dispose()
    {
        _dispatcher.Invoke(() =>
        {
            Engine.Dispose();
            _window?.Close();
        });
        _dispatcher.InvokeShutdown();
        AppSession.DeleteQuietly(_data);
    }

    // Every page gets its control in the window at once and counts as shown: the tests are about the pages, not about
    // which tab is on top.
    private void HostPages()
    {
        foreach (var page in Engine.Pages)
        {
            if (!page.HasControl)
            {
                _pages!.Children.Add(page.Control);
                page.SetShown(true);
            }
        }
    }

    private sealed class UiDispatcher(Dispatcher dispatcher) : IUiDispatcher
    {
        public Task InvokeAsync(Action action) => dispatcher.CheckAccess() ? Run(action) : dispatcher.InvokeAsync(action).Task;

        public void Post(Action action) => dispatcher.BeginInvoke(action);

        private static Task Run(Action action)
        {
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        }
    }
}
