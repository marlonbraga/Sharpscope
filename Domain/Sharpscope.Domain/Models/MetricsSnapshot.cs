using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Metrics snapshot indexed by graph node id.
/// </summary>
public sealed record MetricsSnapshot(
    IReadOnlyDictionary<string, MethodMetrics> Methods,
    IReadOnlyDictionary<string, TypeMetrics> Types,
    IReadOnlyDictionary<string, NamespaceMetrics> Namespaces,
    IReadOnlyDictionary<string, ProjectMetrics> Projects,
    SummaryMetrics Summary,
    IReadOnlyDictionary<string, NamespaceCouplingMetrics> NamespaceCoupling,
    IReadOnlyDictionary<string, TypeCouplingMetrics> TypeCoupling,
    DependencyMetrics Dependencies
)
{
    public static MetricsSnapshot Empty { get; } =
        new(
            new Dictionary<string, MethodMetrics>(),
            new Dictionary<string, TypeMetrics>(),
            new Dictionary<string, NamespaceMetrics>(),
            new Dictionary<string, ProjectMetrics>(),
            SummaryMetrics.Empty,
            new Dictionary<string, NamespaceCouplingMetrics>(),
            new Dictionary<string, TypeCouplingMetrics>(),
            new DependencyMetrics(0, 0, new List<DependencyCycle>())
        );
}

/// <summary>
/// Project-level metrics (placeholder for future extensions).
/// </summary>
public sealed record ProjectMetrics(
    string ProjectId,
    int Namespaces,
    int Types,
    int Methods
);
