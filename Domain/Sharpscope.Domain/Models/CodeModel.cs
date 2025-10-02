using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Complete IR: codebase + dependency graph.
/// </summary>
public sealed record CodeModel(
    Codebase Codebase,
    DependencyGraph DependencyGraph
)
{
    public static CodeModel Empty { get; } =
        new(Codebase.Empty, DependencyGraph.Empty);
}
