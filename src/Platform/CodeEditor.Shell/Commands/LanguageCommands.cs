using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Localization;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Settings;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// UI language commands (ADR 0011): Russian, English or the Windows language. The choice is saved to
/// <c>workbench.language</c> and applied at startup, since windows and menus have already loaded their strings;
/// changing it offers a restart (folder and tabs are restored).
/// </summary>
public sealed class LanguageCommands(
    ISettingsService settings,
    StatusBarViewModel statusBar,
    IDialogService dialogs,
    ICommandService commandService) : IDisposable
{
    private const string LanguagesGroup = "1_languages";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IMenuRegistry menus)
    {
        Add(commands, menus, ShellCommandIds.LanguageAuto, UiLanguage.Auto, Strings.LanguageAuto, Strings.LanguageAutoMenu, Strings.LanguageNameAuto, order: 1);
        Add(commands, menus, ShellCommandIds.LanguageRussian, UiLanguage.Russian, Strings.LanguageRussian, Strings.LanguageRussianMenu, Native(UiLanguage.Russian), order: 2);
        Add(commands, menus, ShellCommandIds.LanguageEnglish, UiLanguage.English, Strings.LanguageEnglish, Strings.LanguageEnglishMenu, Native(UiLanguage.English), order: 3);
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private void Add(ICommandRegistry commands, IMenuRegistry menus, string id, string language, string title, string menuTitle, string name, int order)
    {
        _registrations.Add(commands.Register(new CommandDefinition(id, title, (_, _) => Choose(language, name), Strings.LanguageCategory)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Language, id, LanguagesGroup, order, title: menuTitle)));
    }

    private async ValueTask Choose(string language, string name)
    {
        if (!settings.TrySetUserValue(UiLanguage.SettingKey, language, out var error))
        {
            statusBar.Message = error;
            return;
        }

        if (IsCurrent(language))
        {
            statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.LanguageUnchanged, name);
            return;
        }

        if (dialogs.Confirm(string.Format(CultureInfo.CurrentCulture, Strings.LanguageRestartPrompt, name), Strings.LanguageRestartDetail, Strings.LanguageRestartConfirm))
        {
            await commandService.ExecuteAsync(ShellCommandIds.Restart);
            return;
        }

        statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.LanguageChanged, name);
    }

    // "Auto" resolves against the installed OS language, the same way startup does.
    private static bool IsCurrent(string language) =>
        UiLanguage.Resolve(language, CultureInfo.InstalledUICulture).TwoLetterISOLanguageName == CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    // The language's own name, capitalized, as in the Windows language menu.
    private static string Native(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        var native = culture.NativeName;
        return char.ToUpper(native[0], culture) + native[1..];
    }
}
