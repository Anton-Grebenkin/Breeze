using CodeEditor.Modules.TextEditor.ViewModels;
using ICSharpCode.AvalonEdit.Rendering;

namespace CodeEditor.Modules.TextEditor.Wpf.Rendering;

/// <summary>
/// Attaches agent change decorations to a <see cref="TextView"/>: line backgrounds and removed lines
/// (<see cref="AgentChangeRenderer"/>), spacers for removed lines (<see cref="RemovedLinesSpacerGenerator"/>) and hunk
/// buttons (<see cref="HunkButtonsLayer"/>). Redraws when the ViewModel recomputes hunks.
/// </summary>
internal sealed class AgentChangeDecorator
{
    private readonly TextView _textView;
    private readonly AgentChangeRenderer _renderer;
    private readonly RemovedLinesSpacerGenerator _spacers = new();
    private readonly HunkButtonsLayer _buttons;
    private AgentChangesViewModel? _changes;

    public AgentChangeDecorator(TextView textView)
    {
        _textView = textView;
        _renderer = new AgentChangeRenderer(textView);
        _buttons = new HunkButtonsLayer(textView);
        textView.BackgroundRenderers.Add(_renderer);
        textView.ElementGenerators.Add(_spacers);
        textView.InsertLayer(_buttons, KnownLayer.Caret, LayerInsertionPosition.Above);
    }

    public void Attach(AgentChangesViewModel? changes)
    {
        if (_changes is not null)
        {
            _changes.HunksChanged -= OnHunksChanged;
        }

        _changes = changes;
        if (_changes is not null)
        {
            _changes.HunksChanged += OnHunksChanged;
        }

        Refresh();
    }

    private void OnHunksChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var hunks = _changes?.Hunks ?? [];
        _renderer.Update(hunks);
        _spacers.Update(hunks);
        _buttons.Update(_changes, hunks);
        // Spacers change line heights, so lines must be rebuilt, not just repainted.
        _textView.Redraw();
    }
}
