using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Complete metrics result of the analysis.
/// </summary>
public sealed record MetricsResult(
    SummaryMetrics Summary,
    IReadOnlyList<NamespaceMetrics> Namespaces,
    IReadOnlyList<TypeMetrics> Types,
    IReadOnlyList<MethodMetrics> Methods,
    IReadOnlyList<NamespaceCouplingMetrics> NamespaceCoupling,
    IReadOnlyList<TypeCouplingMetrics> TypeCoupling,
    DependencyMetrics Dependencies
);
