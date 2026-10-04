using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>
/// What the user saw on the approval card. The edit is rebuilt on write, so it's written only if the document hasn't
/// changed since; if the card couldn't be prepared (the file was locked), the call is refused, otherwise the user would
/// approve something they never saw. A call without a card ("Always allow" rule) isn't in the ledger and runs as is.
/// </summary>
internal sealed class PreviewLedger
{
    /// <summary>Cards that never got a call (the user declined) don't pile up forever.</summary>
    private const int MaxEntries = 64;

    // Call → document fingerprint at card time; null if the card couldn't be prepared.
    private readonly ConcurrentDictionary<string, string?> _previews = new(StringComparer.Ordinal);

    public void Shown(DocumentChange change, string fingerprint) => Remember(change, fingerprint);

    public void Failed(DocumentChange change) => Remember(change, null);

    /// <returns><c>false</c> if there was no card for this call.</returns>
    public bool TryTake(DocumentChange change, out string? fingerprint) => _previews.TryRemove(Key(change), out fingerprint);

    private void Remember(DocumentChange change, string? fingerprint)
    {
        if (_previews.Count >= MaxEntries)
        {
            _previews.Clear();
        }

        _previews[Key(change)] = fingerprint;
    }

    // The same call maps to one entry: arguments have the same shape for the card and the write.
    private static string Key(DocumentChange change) => JsonSerializer.Serialize(change, AIJsonUtilities.DefaultOptions);
}
