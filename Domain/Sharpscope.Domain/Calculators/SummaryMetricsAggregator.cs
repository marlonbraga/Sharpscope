using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

/// <summary>
/// Aggregates solution-wide summary metrics from type and method metrics.
/// </summary>
public sealed class SummaryMetricsAggregator
{
    #region Public API

    /// <summary>
    /// Computes <see cref="SummaryMetrics"/> for the whole model using the provided
    /// per-type and per-method metrics.
    /// </summary>
    public SummaryMetrics Compute(
        CodeGraph graph,
        IReadOnlyList<TypeMetrics> types,
        IReadOnlyList<MethodMetrics> methods)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (types is null) throw new ArgumentNullException(nameof(types));
        if (methods is null) throw new ArgumentNullException(nameof(methods));

        var model = CodeGraphModelAdapter.ToCodeModel(graph);
        var totalNamespaces = CountNamespaces(model);
        var totalTypes = types.Count;
        var meanTypesPerNs = SafeDivision(totalTypes, totalNamespaces);

        // SLOC (per type distribution)
        var slocPerType = types.Select(t => t.Sloc).ToList();
        var totalSloc = slocPerType.Sum();
        var avgSloc = slocPerType.Mean();
        var medSloc = slocPerType.Median();
        var stdSloc = slocPerType.StandardDeviation();

        // Methods (per type distribution)
        var methodsPerType = types.Select(t => t.Nom).ToList();
        var totalMethods = graph.Nodes.Values.Count(n => n.Kind == GraphNodeKind.Method);
        if (totalMethods == 0) totalMethods = methods.Count; // fallback if graph is empty
        if (totalMethods == 0) totalMethods = methodsPerType.Sum(); // fallback if empty list passed
        var avgMethods = methodsPerType.Mean();
        var medMethods = methodsPerType.Median();
        var stdMethods = methodsPerType.StandardDeviation();

        // Complexity (WMC per type distribution)
        var wmcPerType = types.Select(t => t.Wmc).ToList();
        var totalComplex = wmcPerType.Sum();
        var avgComplex = wmcPerType.Mean();
        var medComplex = wmcPerType.Median();
        var stdComplex = wmcPerType.StandardDeviation();

        return new SummaryMetrics(
            TotalNamespaces: totalNamespaces,
            TotalTypes: totalTypes,
            MeanTypesPerNamespace: meanTypesPerNs,

            TotalSloc: totalSloc,
            AvgSlocPerType: avgSloc,
            MedianSlocPerType: medSloc,
            StdDevSlocPerType: stdSloc,

            TotalMethods: totalMethods,
            AvgMethodsPerType: avgMethods,
            MedianMethodsPerType: medMethods,
            StdDevMethodsPerType: stdMethods,

            TotalComplexity: totalComplex,
            AvgComplexityPerType: avgComplex,
            MedianComplexityPerType: medComplex,
            StdDevComplexityPerType: stdComplex
        );
    }

    /// <summary>
    /// Legacy overload for direct <see cref="CodeModel"/> inputs (used in regression tests).
    /// </summary>
    public SummaryMetrics Compute(
        CodeModel model,
        IReadOnlyList<TypeMetrics> types,
        IReadOnlyList<MethodMetrics> methods)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        if (types is null) throw new ArgumentNullException(nameof(types));
        if (methods is null) throw new ArgumentNullException(nameof(methods));

        var totalNamespaces = CountNamespaces(model);
        var totalTypes = types.Count;
        var meanTypesPerNs = SafeDivision(totalTypes, totalNamespaces);

        var slocPerType = types.Select(t => t.Sloc).ToList();
        var totalSloc = slocPerType.Sum();
        var avgSloc = slocPerType.Mean();
        var medSloc = slocPerType.Median();
        var stdSloc = slocPerType.StandardDeviation();

        var methodsPerType = types.Select(t => t.Nom).ToList();
        var totalMethods = methods.Count;
        if (totalMethods == 0) totalMethods = methodsPerType.Sum();
        var avgMethods = methodsPerType.Mean();
        var medMethods = methodsPerType.Median();
        var stdMethods = methodsPerType.StandardDeviation();

        var wmcPerType = types.Select(t => t.Wmc).ToList();
        var totalComplex = wmcPerType.Sum();
        var avgComplex = wmcPerType.Mean();
        var medComplex = wmcPerType.Median();
        var stdComplex = wmcPerType.StandardDeviation();

        return new SummaryMetrics(
            TotalNamespaces: totalNamespaces,
            TotalTypes: totalTypes,
            MeanTypesPerNamespace: meanTypesPerNs,

            TotalSloc: totalSloc,
            AvgSlocPerType: avgSloc,
            MedianSlocPerType: medSloc,
            StdDevSlocPerType: stdSloc,

            TotalMethods: totalMethods,
            AvgMethodsPerType: avgMethods,
            MedianMethodsPerType: medMethods,
            StdDevMethodsPerType: stdMethods,

            TotalComplexity: totalComplex,
            AvgComplexityPerType: avgComplex,
            MedianComplexityPerType: medComplex,
            StdDevComplexityPerType: stdComplex
        );
    }

    #endregion

    #region Helpers

    private static int CountNamespaces(CodeModel model) =>
        model.Codebase.Modules.SelectMany(m => m.Namespaces).Count();

    private static double SafeDivision(int numerator, int denominator) =>
        denominator > 0 ? numerator / (double)denominator : 0.0;

    #endregion
}
