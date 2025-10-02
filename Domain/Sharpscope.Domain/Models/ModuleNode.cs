using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Module (project/assembly) of the analyzed solution.
/// </summary>
public sealed record ModuleNode(
    string Name,
    IReadOnlyList<NamespaceNode> Namespaces
);
