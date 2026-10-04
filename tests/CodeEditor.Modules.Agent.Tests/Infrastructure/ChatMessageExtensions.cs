using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

internal static class ChatMessageExtensions
{
    /// <summary>
    /// Message text without the <c>&lt;context&gt;</c> block: for a user message, its first part (the question).
    /// </summary>
    public static string? Question(this ChatMessage message) =>
        message.Role == ChatRole.User ? message.Contents.OfType<TextContent>().FirstOrDefault()?.Text : message.Text;
}
