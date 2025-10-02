using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Namespace node in IR.
/// </summary>
public sealed record NamespaceNode(
    string Name,
    IReadOnlyList<TypeNode> Types
);
