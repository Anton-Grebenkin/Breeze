using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CodeEditor.Testing.Shared;

/// <summary>
/// Tests check Russian UI strings, so the UI culture is set explicitly instead of taken from the machine's Windows
/// (ADR 0011). The formatting culture follows: number formats and the ANSI code page of legacy files must not depend
/// on the machine either (an English CI runner decodes ANSI as Windows-1252). tests/Directory.Build.props links this
/// file into every test project.
/// </summary>
internal static class TestUiCulture
{
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Тестовая сборка: язык нужен до первого теста.")]
    internal static void UseRussian()
    {
        var russian = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentUICulture = russian;
        CultureInfo.CurrentUICulture = russian;
        CultureInfo.DefaultThreadCurrentCulture = russian;
        CultureInfo.CurrentCulture = russian;
    }
}
