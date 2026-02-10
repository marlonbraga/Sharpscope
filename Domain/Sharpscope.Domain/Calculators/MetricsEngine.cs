using System;
using System.Collections.Generic;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

/// <summary>
/// Orchestrates all calculators to produce the final <see cref="MetricsSnapshot"/>.
/// </summary>
public sealed class MetricsEngine : IMetricsEngine
{
    #region Fields

    private readonly MethodsMetricsCalculator _methods;
    private readonly TypesMetricsCalculator _types;
    private readonly NamespacesMetricsCalculator _namespaces;
    private readonly CouplingMetricsCalculator _coupling;
    private readonly DependenciesMetricsCalculator _dependencies;
    private readonly SummaryMetricsAggregator _summary;

    #endregion

    #region Constructors

    public MetricsEngine()
        : this(new MethodsMetricsCalculator(),
               new TypesMetricsCalculator(),
               new NamespacesMetricsCalculator(),
               new CouplingMetricsCalculator(),
               new DependenciesMetricsCalculator(),
               new SummaryMetricsAggregator())
    { }

    public MetricsEngine(
        MethodsMetricsCalculator methods,
        TypesMetricsCalculator types,
        NamespacesMetricsCalculator namespaces,
        CouplingMetricsCalculator coupling,
        DependenciesMetricsCalculator dependencies,
        SummaryMetricsAggregator summary)
    {
        _methods = methods ?? throw new ArgumentNullException(nameof(methods));
        _types = types ?? throw new ArgumentNullException(nameof(types));
        _namespaces = namespaces ?? throw new ArgumentNullException(nameof(namespaces));
        _coupling = coupling ?? throw new ArgumentNullException(nameof(coupling));
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _summary = summary ?? throw new ArgumentNullException(nameof(summary));
    }

    #endregion

    #region IMetricsEngine

    public MetricsSnapshot Compute(CodeGraph graph)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));

        // 1) per-entity metrics
        var methodMetrics = _methods.ComputeAll(graph);
        var typeMetrics = _types.ComputeAll(graph);
        var nsMetrics = _namespaces.ComputeAll(graph);

        // 2) coupling & dependencies
        var nsCoupling = _coupling.ComputeNamespaceCoupling(graph);
        var typeCoupling = _coupling.ComputeTypeCoupling(graph);
        var dependencies = _dependencies.Compute(graph);

        // 3) summary
        var summary = _summary.Compute(graph, typeMetrics, methodMetrics);

        return MetricsSnapshotBuilder.Build(graph, summary, nsMetrics, typeMetrics, methodMetrics, nsCoupling, typeCoupling, dependencies);
    }

    #endregion
}
