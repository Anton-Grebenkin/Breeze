namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>
/// An image for the model from a tool (ADR 0030): a browser screenshot, an image from the folder. Not every protocol
/// accepts images in tool results, so the agent sends it as the next message, like a user attachment: the tool shows
/// the image, the call loop ends after the current calls, and the model gets it in a new request.
/// </summary>
public interface IAgentImages
{
    /// <summary>The current model sees images; otherwise there is nothing to show it.</summary>
    bool CanShow { get; }

    /// <param name="name">Where the image comes from: a file path or a page address.</param>
    /// <returns><c>false</c> if the image exceeds the model limit (5 MB) and was not shown.</returns>
    bool TryShow(string name, byte[] data, string mediaType);
}
