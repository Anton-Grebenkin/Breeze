using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace CodeEditor.UI.Controls;

/// <summary>
/// A <see cref="Border"/> visible to UI Automation, so screen readers and UI tests see its <c>AutomationId</c> and
/// name. A plain <see cref="Border"/> doesn't appear in the automation tree.
/// </summary>
public sealed class AutomationBorder : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new AutomationBorderPeer(this);

    private sealed class AutomationBorderPeer(AutomationBorder owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override string GetClassNameCore() => nameof(AutomationBorder);
    }
}
