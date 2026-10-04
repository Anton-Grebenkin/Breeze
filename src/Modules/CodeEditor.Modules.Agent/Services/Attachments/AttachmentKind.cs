namespace CodeEditor.Modules.Agent.Services.Attachments;

/// <summary>Kind of file attached to a message for the agent (<see cref="AttachmentReader"/>).</summary>
public enum AttachmentKind
{
    /// <summary>Not attached: missing, too large, an archive, an Office document, a PDF.</summary>
    Unsupported,

    /// <summary>Text, sent as content in the message.</summary>
    Text,

    /// <summary>An image, sent as a picture for the model.</summary>
    Image,
}
