using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using CodeEditor.Modules.Documents.Resources;
using CodeEditor.Modules.Documents.ViewModels;
using CodeEditor.Modules.Documents.Wpf.Services;
using CodeEditor.Shell.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CodeEditor.Modules.Documents.Wpf.Views;

/// <summary>
/// PDF view in the built-in WebView2 viewer. The browser is created when the tab is first shown, in the environment
/// shared by all PDF tabs (<see cref="PdfEnvironment"/>); each ViewModel reload shows the file again. Uses the same
/// <see cref="WebView2"/> control as the browser and diagram preview: the composition-rendered variant needs the Windows
/// SDK projection (about 24 MB), which the app doesn't ship. Navigations away from the PDF and new windows go to the
/// system browser. Closing the tab releases the browser.
/// </summary>
public sealed partial class PdfViewerView
{
    private readonly PdfEnvironment _environment;
    private readonly ISystemShell _shell;
    private PdfViewerViewModel? _viewModel;
    private WebView2? _browser;
    private bool _starting;

    public PdfViewerView(PdfEnvironment environment, ISystemShell shell)
    {
        _environment = environment;
        _shell = shell;
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Release();
        IsVisibleChanged += OnVisibleChanged;
    }

    private void Attach()
    {
        var viewModel = IsLoaded ? DataContext as PdfViewerViewModel : null;
        if (ReferenceEquals(viewModel, _viewModel))
        {
            return;
        }

        Detach();
        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelChanged;
            OnVisibleChanged(this, default);
        }
    }

    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel = null;
        }
    }

    private void Release()
    {
        Detach();
        Host.Content = null;
        _browser?.Dispose();
        _browser = null;
    }

    // async void event handler: loading and browser creation catch their own errors.
    private async void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible || _viewModel is not { } viewModel)
        {
            return;
        }

        await viewModel.EnsureLoadedAsync();

        // Only a new browser opens the file: an existing one already shows it, so returning to the tab keeps the page
        // and zoom. A changed file is reloaded via Revision.
        if (await CreateBrowserAsync())
        {
            Navigate();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PdfViewerViewModel.Revision))
        {
            Navigate();
        }
    }

    /// <returns><c>true</c> if the browser was created now; <c>false</c> if it exists, is starting or failed.</returns>
    private async Task<bool> CreateBrowserAsync()
    {
        if (_browser is not null || _starting)
        {
            return false;
        }

        _starting = true;
        var browser = new WebView2();
        AutomationProperties.SetAutomationId(browser, "PdfViewer.Page");
        AutomationProperties.SetName(browser, Strings.Document);
        try
        {
            Host.Content = browser;
            await browser.EnsureCoreWebView2Async(await _environment.GetAsync());
            if (!IsLoaded)
            {
                browser.Dispose();
                return false;
            }

            Configure(browser.CoreWebView2);
            _browser = browser;
            Unavailable.Visibility = Visibility.Collapsed;
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Host.Content = null;
            browser.Dispose();
            Unavailable.Text = string.Format(CultureInfo.CurrentCulture, Strings.PdfUnavailable, exception.Message);
            Unavailable.Visibility = Visibility.Visible;
            return false;
        }
        finally
        {
            _starting = false;
        }
    }

    private void Configure(CoreWebView2 core)
    {
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
    }

    // Navigating to the same address reloads the file in the viewer.
    private void Navigate()
    {
        if (_browser?.CoreWebView2 is { } core && _viewModel?.Source is { } source)
        {
            core.Navigate(source.AbsoluteUri);
        }
    }

    // The tab shows only its own file; any other address is a link from the document.
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) && target.IsFile
            && string.Equals(target.LocalPath, _viewModel?.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;
        OpenLink(e.Uri);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenLink(e.Uri);
    }

    // PDF links open in the system browser, web and mailto only.
    private void OpenLink(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
        {
            _shell.OpenInBrowser(uri);
        }
    }
}
