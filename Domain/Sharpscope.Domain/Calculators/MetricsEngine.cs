using System;
using System.Collections.Generic;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

/// <summary>
/// Orchestrates all calculators to produce the final <see cref="MetricsResult"/>.
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

    public MetricsResult Compute(CodeModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        // 1) per-entity metrics
        var methodMetrics = _methods.ComputeAll(model);
        var typeMetrics = _types.ComputeAll(model);
        var nsMetrics = _namespaces.ComputeAll(model);

        // 2) coupling & dependencies
        var nsCoupling = _coupling.ComputeNamespaceCoupling(model);
        var typeCoupling = _coupling.ComputeTypeCoupling(model);
        var dependencies = _dependencies.Compute(model);

        // 3) summary
        var summary = _summary.Compute(model, typeMetrics, methodMetrics);

        return new MetricsResult(
            Summary: summary,
            Namespaces: nsMetrics,
            Types: typeMetrics,
            Methods: methodMetrics,
            NamespaceCoupling: nsCoupling,
            TypeCoupling: typeCoupling,
            Dependencies: dependencies
        );
    }

    #endregion
}
