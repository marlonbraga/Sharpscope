using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.DomainTests;

public sealed class DependenciesMetricsCalculatorTests
{
    [Fact(DisplayName = "Compute returns totals and cycles for a graph with type and namespace cycles")]
    public void Compute_WithCycles_Works()
    {
        // Types
        var a = new TypeNode("N1.A", TypeKind.Class, false,
            new List<FieldNode>(), new List<MethodNode>(),
            new List<string> { "N1.B", "System.String" }); // external + internal

        var b = new TypeNode("N1.B", TypeKind.Class, false,
            new List<FieldNode>(), new List<MethodNode>(),
            new List<string> { "N1.A" }); // closes type cycle A<->B

        var c = new TypeNode("N2.C", TypeKind.Class, false,
            new List<FieldNode>(), new List<MethodNode>(),
            new List<string>());

        var ns1 = new NamespaceNode("N1", new List<TypeNode> { a, b });
        var ns2 = new NamespaceNode("N2", new List<TypeNode> { c });
        var module = new ModuleNode("M", new List<NamespaceNode> { ns1, ns2 });
        var model = new CodeModel(
            new Codebase(new List<ModuleNode> { module }),
            new DependencyGraph(
                // Internal type graph (A<->B), C isolated
                new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["N1.A"] = new HashSet<string> { "N1.B" },
                    ["N1.B"] = new HashSet<string> { "N1.A" },
                    ["N2.C"] = new HashSet<string>()
                },
                // Namespace graph cycle N1<->N2
                new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["N1"] = new HashSet<string> { "N2" },
                    ["N2"] = new HashSet<string> { "N1" }
                }
            )
        );

        var calc = new DependenciesMetricsCalculator();

        // Act
        var dm = calc.Compute(model);

        // Assert
        // DEP from nodes: A->B, A->System.String, B->A  => 3 distinct
        dm.TotalDependencies.ShouldBe(3);

        // I-DEP from internal graph: A->B, B->A  => 2 distinct
        dm.InternalDependencies.ShouldBe(2);

        // Cycles: one Type cycle [A,B] and one Namespace cycle [N1,N2]
        dm.Cycles.Count.ShouldBe(2);
        dm.Cycles.Count(cyc => cyc.Scope == "Type").ShouldBe(1);
        dm.Cycles.Count(cyc => cyc.Scope == "Namespace").ShouldBe(1);

        var typeCycle = dm.Cycles.First(c => c.Scope == "Type").Nodes;
        typeCycle.ShouldContain("N1.A");
        typeCycle.ShouldContain("N1.B");
        typeCycle.Count.ShouldBe(2);

        var nsCycle = dm.Cycles.First(c => c.Scope == "Namespace").Nodes;
        nsCycle.ShouldBe(new[] { "N1", "N2" }, ignoreOrder: true);
    }

    [Fact(DisplayName = "Compute returns zeros for empty model")]
    public void Compute_Empty_Works()
    {
        var emptyModel = new CodeModel(
            new Codebase(new List<ModuleNode> { new("M", new List<NamespaceNode>()) }),
            DependencyGraph.Empty);

        var calc = new DependenciesMetricsCalculator();

        var dm = calc.Compute(emptyModel);

        dm.TotalDependencies.ShouldBe(0);
        dm.InternalDependencies.ShouldBe(0);
        dm.Cycles.Count.ShouldBe(0);
    }
}
