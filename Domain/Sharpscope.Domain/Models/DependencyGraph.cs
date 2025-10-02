using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Dependency graphs for types and namespaces.
/// </summary>
public sealed record DependencyGraph(
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> TypeEdges,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> NamespaceEdges
)
{
    /// <summary>
    /// Empty dependency graph (no edges).
    /// </summary>
    public static DependencyGraph Empty { get; } =
        new DependencyGraph(
            new Dictionary<string, IReadOnlyCollection<string>>(),
            new Dictionary<string, IReadOnlyCollection<string>>()
        );
}
