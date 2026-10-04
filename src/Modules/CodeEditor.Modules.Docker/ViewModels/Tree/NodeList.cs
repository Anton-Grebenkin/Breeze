using System.Collections.ObjectModel;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>
/// Updates a node list from a new snapshot without recreating nodes: an existing node is found by key, and the list
/// changes by minimal inserts, removals and moves, so the view gets only real changes.
/// </summary>
internal static class NodeList
{
    public static Dictionary<string, DockerNodeViewModel> ByKey(IEnumerable<DockerNodeViewModel> nodes) =>
        nodes.ToDictionary(node => node.Key, StringComparer.Ordinal);

    /// <summary>The existing node with this key, or a new one.</summary>
    public static TNode Reuse<TNode>(Dictionary<string, DockerNodeViewModel> existing, string key, Func<TNode> create)
        where TNode : DockerNodeViewModel =>
        existing.TryGetValue(key, out var node) && node is TNode reused ? reused : create();

    /// <summary>Makes <paramref name="nodes"/> match <paramref name="desired"/>.</summary>
    /// <remarks>O(n) while the order holds; a moved node is found linearly, as lists hold tens of nodes.</remarks>
    public static void Apply(ObservableCollection<DockerNodeViewModel> nodes, IReadOnlyList<DockerNodeViewModel> desired)
    {
        var keep = new HashSet<DockerNodeViewModel>(desired, ReferenceEqualityComparer.Instance);
        for (var position = nodes.Count - 1; position >= 0; position--)
        {
            if (!keep.Contains(nodes[position]))
            {
                nodes.RemoveAt(position);
            }
        }

        for (var position = 0; position < desired.Count; position++)
        {
            var node = desired[position];
            if (position < nodes.Count && ReferenceEquals(nodes[position], node))
            {
                continue;
            }

            var current = nodes.IndexOf(node);
            if (current >= 0)
            {
                nodes.Move(current, position);
            }
            else
            {
                nodes.Insert(position, node);
            }
        }
    }
}
