using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Aggregated dependency metrics.
/// </summary>
public sealed record DependencyMetrics(
    int TotalDependencies,            // DEP
    int InternalDependencies,         // I-DEP
    IReadOnlyList<DependencyCycle> Cycles
);
