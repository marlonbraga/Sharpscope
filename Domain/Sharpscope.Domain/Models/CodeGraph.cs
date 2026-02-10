using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Canonical, reconstructible graph extracted from code.
/// </summary>
public sealed record CodeGraph(
    IReadOnlyDictionary<string, GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges
)
{
    public static CodeGraph Empty { get; } =
        new(
            new Dictionary<string, GraphNode>(),
            new List<GraphEdge>()
        );
}

public sealed record GraphNode(
    string Id,
    GraphNodeKind Kind,
    string Name,
    IReadOnlyDictionary<string, string> Attributes,
    string? Evidence = null
);

public sealed record GraphEdge(
    string FromId,
    string ToId,
    GraphEdgeKind Kind,
    string? Label,
    IReadOnlyDictionary<string, string> Attributes,
    string? Evidence,
    double Confidence
);

public enum GraphNodeKind
{
    Solution,
    Project,
    Namespace,
    Type,
    Method,
    ExternalIntegration,
    MessageChannel
}

public enum GraphEdgeKind
{
    Contains,
    DeclaredIn,
    Calls,
    ReferencesType,
    Inherits,
    Implements,
    UsesExternal,
    PublishesTo,
    ConsumesFrom
}
