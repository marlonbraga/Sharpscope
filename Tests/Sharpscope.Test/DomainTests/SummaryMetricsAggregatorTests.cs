using System.Collections.Generic;
using Shouldly;
using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.DomainTests;

public sealed class SummaryMetricsAggregatorTests
{
    [Fact(DisplayName = "Compute returns expected summary metrics for a small model")]
    public void Compute_SmallModel_Works()
    {
        // Model: 2 namespaces, 3 types.
        // SLOC per type: [10, 20, 30]  -> total 60, avg 20, median 20, stdev ≈ 8.1649658
        // NOM per type:  [2, 3, 1]     -> total 6,  avg 2,  median 2,  stdev ≈ 0.8164966
        // WMC per type:  [3, 5, 2]     -> total 10, avg 3.333..., median 3, stdev ≈ 1.2472191

        var ns1 = new NamespaceNode("N1", new List<TypeNode>
        {
            new("N1.T1", TypeKind.Class, false,
                new List<FieldNode>(), new List<MethodNode>(), new List<string>()),
            new("N1.T2", TypeKind.Class, false,
                new List<FieldNode>(), new List<MethodNode>(), new List<string>()),
        });

        var ns2 = new NamespaceNode("N2", new List<TypeNode>
        {
            new("N2.T3", TypeKind.Class, false,
                new List<FieldNode>(), new List<MethodNode>(), new List<string>()),
        });

        var module = new ModuleNode("M1", new List<NamespaceNode> { ns1, ns2 });
        var codebase = new Codebase(new List<ModuleNode> { module });
        var model = new CodeModel(codebase, DependencyGraph.Empty);

        // Type metrics for 3 types
        var types = new List<TypeMetrics>
        {
            new("N1.T1", 10, 2, 1, 3,  2, 1, 0, 1, 0, 0.0),
            new("N1.T2", 20, 3, 2, 5,  1, 1, 1, 0, 0, 0.0),
            new("N2.T3", 30, 1, 1, 2,  0, 0, 1, 0, 0, 0.0),
        };

        // Method metrics (authoritative count)
        var methods = new List<MethodMetrics>
        {
            new("N1.T1.M1", 5, 2, 1, 1, 0),
            new("N1.T1.M2", 5, 1, 0, 0, 2),
            new("N1.T2.M1", 7, 3, 2, 1, 1),
            new("N1.T2.M2", 6, 2, 1, 0, 1),
            new("N1.T2.M3", 7, 0, 0, 0, 1),
            new("N2.T3.M1", 3, 2, 0, 0, 1),
        };

        var agg = new SummaryMetricsAggregator();

        // Act
        var s = agg.Compute(model, types, methods);

        // Assert — namespaces/types
        s.TotalNamespaces.ShouldBe(2);
        s.TotalTypes.ShouldBe(3);
        s.MeanTypesPerNamespace.ShouldBe(1.5, 1e-9);

        // SLOC
        s.TotalSloc.ShouldBe(60);
        s.AvgSlocPerType.ShouldBe(20.0, 1e-9);
        s.MedianSlocPerType.ShouldBe(20.0, 1e-9);
        s.StdDevSlocPerType.ShouldBe(8.1649658, 1e-6);

        // Methods
        s.TotalMethods.ShouldBe(6);
        s.AvgMethodsPerType.ShouldBe(2.0, 1e-9);
        s.MedianMethodsPerType.ShouldBe(2.0, 1e-9);
        s.StdDevMethodsPerType.ShouldBe(0.81649658, 1e-6);

        // Complexity (WMC)
        s.TotalComplexity.ShouldBe(10);
        s.AvgComplexityPerType.ShouldBe(10.0 / 3.0, 1e-9);
        s.MedianComplexityPerType.ShouldBe(3.0, 1e-9);
        s.StdDevComplexityPerType.ShouldBe(1.2472191, 1e-6);
    }

    [Fact(DisplayName = "Compute returns zeros for empty input")]
    public void Compute_EmptyModel_ReturnsZeros()
    {
        var emptyModule = new ModuleNode("M", new List<NamespaceNode>());
        var codebase = new Codebase(new List<ModuleNode> { emptyModule });
        var model = new CodeModel(codebase, DependencyGraph.Empty);

        var types = new List<TypeMetrics>();
        var methods = new List<MethodMetrics>();

        var agg = new SummaryMetricsAggregator();

        var s = agg.Compute(model, types, methods);

        s.TotalNamespaces.ShouldBe(0);
        s.TotalTypes.ShouldBe(0);
        s.MeanTypesPerNamespace.ShouldBe(0.0, 1e-12);

        s.TotalSloc.ShouldBe(0);
        s.AvgSlocPerType.ShouldBe(0.0, 1e-12);
        s.MedianSlocPerType.ShouldBe(0.0, 1e-12);
        s.StdDevSlocPerType.ShouldBe(0.0, 1e-12);

        s.TotalMethods.ShouldBe(0);
        s.AvgMethodsPerType.ShouldBe(0.0, 1e-12);
        s.MedianMethodsPerType.ShouldBe(0.0, 1e-12);
        s.StdDevMethodsPerType.ShouldBe(0.0, 1e-12);

        s.TotalComplexity.ShouldBe(0);
        s.AvgComplexityPerType.ShouldBe(0.0, 1e-12);
        s.MedianComplexityPerType.ShouldBe(0.0, 1e-12);
        s.StdDevComplexityPerType.ShouldBe(0.0, 1e-12);
    }
}
