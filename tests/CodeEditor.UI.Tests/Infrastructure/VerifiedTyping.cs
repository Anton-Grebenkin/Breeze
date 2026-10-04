using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// Verified typing: under load Windows sometimes drops or garbles a Cyrillic character sent via SendInput, failing the
/// test for reasons unrelated to the app. The field is read back; on mismatch our input is erased and retyped. Text
/// that was in the field before (e.g. the palette's ">") is kept.
/// </summary>
public static class VerifiedTyping
{
    private const int Attempts = 3;
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(2);

    public static void TypeInto(this AppSession session, string automationId, string text)
    {
        ArgumentNullException.ThrowIfNull(session);
        var prefix = session.ValueOf(automationId);
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            Keyboard.Type(text);
            if (Retry.WhileFalse(() => session.ValueOf(automationId) == prefix + text, SettleTimeout).Result)
            {
                return;
            }

            var typed = Math.Max(0, session.ValueOf(automationId).Length - prefix.Length);
            Keyboard.Type([.. Enumerable.Repeat(VirtualKeyShort.BACK, typed)]);
        }

        throw new InvalidOperationException($"UI-тест: в {automationId} не удалось ввести «{text}», в поле «{session.ValueOf(automationId)}».");
    }
}
