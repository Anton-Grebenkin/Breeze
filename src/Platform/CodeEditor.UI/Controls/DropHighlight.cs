using System.Windows;

namespace CodeEditor.UI.Controls;

/// <summary>
/// Marks the element a drag would drop into, such as the target folder of the file tree; the tree styles paint it with
/// <c>Brush.List.DropBackground</c>, like <c>list.dropBackground</c> in VS Code.
/// </summary>
public static class DropHighlight
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
        "IsActive", typeof(bool), typeof(DropHighlight), new PropertyMetadata(false));

    public static bool GetIsActive(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsActiveProperty);
    }

    public static void SetIsActive(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsActiveProperty, value);
    }
}
