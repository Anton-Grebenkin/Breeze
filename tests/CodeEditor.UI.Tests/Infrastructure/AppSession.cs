using System.Diagnostics;
using System.Reflection;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// A running app instance with access to the main window via UI Automation. One instance per test class
/// (<see cref="IClassFixture{TFixture}"/>).
/// </summary>
public sealed class AppSession : IDisposable
{
    private const string AppExePathKey = "AppExePath";

    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    private const string UserDataVariable = "CODEEDITOR_USER_DATA";
    private const string BindingErrorsVariable = "CODEEDITOR_BINDING_ERRORS_FILE";
    private const string LanguageVariable = "CODEEDITOR_UI_LANGUAGE";

    /// <summary>Tests also find elements by Russian text, so the language is fixed regardless of the Windows locale.</summary>
    private const string TestLanguage = "ru";

    private readonly Application _application;
    private readonly UIA3Automation _automation = new();
    private readonly bool _ownsUserData;
    private readonly string _bindingErrorsFile = Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", $"binding-errors-{Guid.NewGuid():N}.log");

    /// <summary>Starts with a clean user data folder, so no saved layout affects the tests.</summary>
    public AppSession()
        : this(CreateUserDataFolder(), ownsUserData: true)
    {
    }

    /// <summary>Starts with a given data folder (and arguments), to check that state survives a restart.</summary>
    public static AppSession WithUserData(string userDataFolder, params string[] arguments) => new(userDataFolder, ownsUserData: false, arguments);

    /// <summary>Starts with command-line arguments (e.g. a folder to open) and clean data.</summary>
    public static AppSession WithArguments(params string[] arguments) =>
        new(CreateUserDataFolder(), ownsUserData: true, arguments);

    private AppSession(string userDataFolder, bool ownsUserData, params string[] arguments)
    {
        UserDataFolder = userDataFolder;
        _ownsUserData = ownsUserData;

        var startInfo = new ProcessStartInfo(AppExePath());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[UserDataVariable] = userDataFolder;
        startInfo.Environment[LanguageVariable] = TestLanguage;
        Directory.CreateDirectory(Path.GetDirectoryName(_bindingErrorsFile)!);
        startInfo.Environment[BindingErrorsVariable] = _bindingErrorsFile;

        _application = Application.Launch(startInfo);
        MainWindow = _application.GetMainWindow(_automation, LaunchTimeout)
            ?? throw new InvalidOperationException("Главное окно не появилось.");
        InputGuard.Attach(_application.ProcessId);
    }

    public string UserDataFolder { get; }

    public static string CreateUserDataFolder() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"))).FullName;

    public Window MainWindow { get; }

    public int ProcessId => _application.ProcessId;

    // Generous: in a full solution run UI tests share a busy machine with other test assemblies.
    private static readonly TimeSpan ElementTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ScreenshotSettleDelay = TimeSpan.FromMilliseconds(400);

    public AutomationElement Find(string automationId) =>
        TryFind(automationId) ?? throw new InvalidOperationException($"Элемент {automationId} не найден.");

    /// <summary>Finds an element in all app windows: WPF popup menus are separate top-level windows.</summary>
    public AutomationElement? TryFind(string automationId)
    {
        var inMainWindow = MainWindow.FindFirstDescendant(condition => condition.ByAutomationId(automationId));
        if (inMainWindow is not null)
        {
            return inMainWindow;
        }

        return ProcessWindows.VisibleTopLevel(ProcessId)
            .Where(handle => handle != MainWindow.Properties.NativeWindowHandle.ValueOrDefault)
            .Select(handle => _automation.FromHandle(handle))
            .Select(window => window.FindFirstDescendant(condition => condition.ByAutomationId(automationId)))
            .FirstOrDefault(element => element is not null);
    }

    /// <summary>Waits for an element to appear (e.g. a menu or the palette opening).</summary>
    public AutomationElement WaitFor(string automationId)
    {
        AutomationElement? element = null;
        Expect(Retry.WhileNull(() => element = TryFind(automationId), ElementTimeout).Success, $"не появился {automationId}");
        return element!;
    }

    /// <summary>Waits until the element has keyboard focus; only then does input reach it.</summary>
    public AutomationElement WaitForFocus(string automationId)
    {
        var element = WaitFor(automationId);
        Expect(Retry.WhileFalse(() => element.Properties.HasKeyboardFocus.ValueOrDefault, ElementTimeout).Success, $"нет фокуса у {automationId}");
        return element;
    }

    /// <summary>Waits until the element disappears (the palette or a menu closed).</summary>
    public void WaitUntilGone(string automationId) =>
        Expect(Retry.WhileFalse(() => TryFind(automationId) is null, ElementTimeout).Success, $"не исчез {automationId}");

    /// <summary>A failed wait leaves a window screenshot showing what got in the way.</summary>
    private void Expect(bool success, string failure)
    {
        if (success)
        {
            return;
        }

        var screenshot = SaveScreenshot($"timeout-{DateTime.Now:HHmmss}");
        throw new TimeoutException($"UI-тест: {failure} за {ElementTimeout.TotalSeconds} с. Снимок: {screenshot}");
    }

    /// <summary>Focuses the window so key presses reach the app.</summary>
    public void FocusWindow()
    {
        MainWindow.SetForeground();
        MainWindow.Focus();
    }

    /// <summary>Saves a window screenshot to <c>screenshots/</c> next to the test assembly, for visual review.</summary>
    public string SaveScreenshot(string name)
    {
        // Popup menus animate in; without a pause the screenshot catches a semi-transparent frame.
        Thread.Sleep(ScreenshotSettleDelay);

        var folder = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"{name}.png");
        Capture.Element(MainWindow).ToFile(path);
        return path;
    }

    /// <summary>The input's current value (ValuePattern).</summary>
    public string ValueOf(string automationId) => WaitFor(automationId).Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty;

    /// <summary>Waits until the input's value (ValuePattern) satisfies the predicate.</summary>
    public string WaitForValue(string automationId, Func<string, bool> predicate, TimeSpan timeout)
    {
        var value = string.Empty;
        Retry.WhileFalse(
            () => predicate(value = WaitFor(automationId).Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty),
            timeout,
            throwOnTimeout: true);
        return value;
    }

    /// <summary>Waits until the element's text satisfies the predicate and returns it.</summary>
    public string WaitForText(string automationId, Func<string, bool> predicate, TimeSpan timeout)
    {
        var text = string.Empty;
        Retry.WhileFalse(() => predicate(text = Find(automationId).Name), timeout, throwOnTimeout: true);
        return text;
    }

    public void Dispose()
    {
        InputGuard.Detach(_application.ProcessId);
        if (!_application.HasExited)
        {
            _application.Close();
            Retry.WhileFalse(() => _application.HasExited, CloseTimeout);
        }

        if (!_application.HasExited)
        {
            _application.Kill();
        }

        _application.Dispose();
        _automation.Dispose();

        if (_ownsUserData)
        {
            DeleteQuietly(UserDataFolder);
        }

        // A WPF binding error is a code bug even if the scenario passed (e.g. an empty context menu).
        if (File.Exists(_bindingErrorsFile))
        {
            var errors = File.ReadAllText(_bindingErrorsFile);
            File.Delete(_bindingErrorsFile);
            throw new InvalidOperationException($"Ошибки привязок WPF:{Environment.NewLine}{errors}");
        }
    }

    /// <summary>The latest log file in the data folder; readable even while the app writes to it.</summary>
    public static string ReadLog(string userDataFolder)
    {
        var path = Directory.GetFiles(Path.Combine(userDataFolder, "logs"), "codeeditor-*.log").OrderByDescending(File.GetLastWriteTimeUtc).First();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void DeleteQuietly(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A temp folder held by an antivirus or indexer will be cleaned up by the system.
        }
    }

    /// <summary>
    /// Starts another launch of the app with the same data folder and language, without waiting for a window: it may
    /// hand its request to a running window and exit (ADR 0044).
    /// </summary>
    public static Process Launch(string userDataFolder, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(AppExePath());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[UserDataVariable] = userDataFolder;
        startInfo.Environment[LanguageVariable] = TestLanguage;
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Процесс не запустился.");
    }

    /// <summary>Path of the built app the tests launch.</summary>
    public static string AppExePath()
    {
        var path = typeof(AppSession).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == AppExePathKey)
            .Value ?? string.Empty;

        return File.Exists(path)
            ? path
            : throw new FileNotFoundException("Приложение не собрано. Сначала выполни dotnet build.", path);
    }
}
