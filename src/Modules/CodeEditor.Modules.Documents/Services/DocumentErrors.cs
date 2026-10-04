using CodeEditor.Modules.Agent.Contracts;

namespace CodeEditor.Modules.Documents.Services;

/// <summary>
/// A failure to parse a third-party file is not an editor bug: damaged XML inside a document or an unexpected PDF
/// structure doesn't crash the tool call or the tab but shows as "can't read the file". Cancellation and model-facing
/// errors (<see cref="AgentToolException"/>) pass through as is.
/// </summary>
public static class DocumentErrors
{
    public static bool IsReadFailure(Exception exception) => exception is not (OperationCanceledException or AgentToolException);
}
