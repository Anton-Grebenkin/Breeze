using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Turn;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Services.Prompts;
using CodeEditor.Modules.Agent.Services.Watchdog;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Watchdog;

/// <summary>Editor notes on tool results: module events and the watchdog, as one note the model reads at once.</summary>
public sealed class ToolCallObserversTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void ModuleNotes_GoIntoOneEditorNote_AndThoseFromBetweenTurnsAreDropped()
    {
        var module = new QueuedNotes();
        var observers = new ToolCallObservers(new TurnWatchdog(_fixture.Budget), [module]);
        module.Add("старое");

        observers.Begin();
        var quiet = observers.After(new ToolCallRecord("read_file", new Dictionary<string, object?>(), "ok", Failed: false, TimeSpan.Zero));
        module.Add("Фоновая команда #2 завершилась.");
        var noted = observers.After(new ToolCallRecord("read_file", new Dictionary<string, object?>(), "ok", Failed: false, TimeSpan.Zero));

        Assert.Equal(string.Empty, quiet);
        Assert.Equal("\n\n" + PromptSections.EditorNote("Фоновая команда #2 завершилась."), noted);
    }

    [Fact]
    public async Task SameFailedEditTwice_ResultTellsToChangeTheCall()
    {
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create(
            string (string path, string text) => throw new AgentToolException($"фрагмент «{text}» встречается 2 раза (строки 3, 9)"), "edit_text"));
        _fixture.Client
            .CallTool("c1", "edit_text", new Dictionary<string, object?> { ["path"] = "src/A.cs", ["text"] = "a" })
            .CallTool("c2", "edit_text", new Dictionary<string, object?> { ["path"] = "src/A.cs", ["text"] = "b" })
            .Reply("Не вышло.");

        await _fixture.SendAsync("поправь");

        var results = _fixture.Client.Requests[^1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Select(result => result.Result?.ToString() ?? string.Empty).ToList();
        var advice = string.Format(CultureInfo.CurrentCulture, Strings.WatchdogSameError, "edit_text", "src/A.cs");
        Assert.DoesNotContain(advice, results[0], StringComparison.Ordinal);
        Assert.Contains(advice, results[1], StringComparison.Ordinal);
    }

    private sealed class QueuedNotes : IAgentTurnNotes
    {
        private readonly List<string> _notes = [];

        public void Add(string note) => _notes.Add(note);

        public IReadOnlyList<string> TakeNotes()
        {
            var notes = _notes.ToList();
            _notes.Clear();
            return notes;
        }
    }
}
