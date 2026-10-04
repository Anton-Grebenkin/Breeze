namespace CodeEditor.Shell.Menus;

/// <summary>
/// A group separator is visible only with visible items on both sides: a group hidden by <c>when</c> leaves no double
/// separator, and a menu never starts or ends with one. Single pass, O(n).
/// </summary>
internal static class MenuSeparators
{
    public static void Update(IReadOnlyList<MenuItemViewModel> items)
    {
        MenuItemViewModel? pending = null;
        var afterItem = false;
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                item.IsVisible = false;
                pending ??= afterItem ? item : null;
                continue;
            }

            if (!item.IsVisible)
            {
                continue;
            }

            if (pending is not null)
            {
                pending.IsVisible = true;
                pending = null;
            }

            afterItem = true;
        }
    }
}
