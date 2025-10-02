using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// A dependency cycle detected. Scope: "Type" or "Namespace".
/// </summary>
public sealed record DependencyCycle(
    IReadOnlyList<string> Nodes,
    string Scope
);
