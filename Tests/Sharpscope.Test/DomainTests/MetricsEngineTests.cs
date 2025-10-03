using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.DomainTests;

public sealed class MetricsEngineTests
{
    [Fact(DisplayName = "Compute orchestrates calculators and returns consistent aggregate metrics")]
    public void Compute_SimpleModel_Works()
    {
        // Build a small model:
        // Namespaces: N1 (A, B), N2 (C)
        // Type graph: A -> C (internal)
        // Namespace graph: N1 -> N2
        // A also depends on System.String (external)
        var mA1 = new MethodNode("N1.A.M1", 0, 5, 1, 1, 0, true, new List<string> { "F1" });
        var mA2 = new MethodNode("N1.A.M2", 0, 3, 0, 0, 0, false, new List<string>());

        var typeA = new TypeNode("N1.A", TypeKind.Class, false,
            new List<FieldNode> { new("F1", "int", true) },
            new List<MethodNode> { mA1, mA2 },
            new List<string> { "N2.C", "System.String" });

        var typeB = new TypeNode("N1.B", TypeKind.Class, false,
            new List<FieldNode>(),
            new List<MethodNode> { new("N1.B.M", 0, 4, 0, 0, 0, true, new List<string>()) },
            new List<string>());

        var typeC = new TypeNode("N2.C", TypeKind.Class, false,
            new List<FieldNode> { new("G", "int", true) },
            new List<MethodNode> { new("N2.C.N", 0, 3, 0, 0, 0, true, new List<string> { "G" }) },
            new List<string>());

        var ns1 = new NamespaceNode("N1", new List<TypeNode> { typeA, typeB });
        var ns2 = new NamespaceNode("N2", new List<TypeNode> { typeC });
        var module = new ModuleNode("M", new List<NamespaceNode> { ns1, ns2 });

        var codebase = new Codebase(new List<ModuleNode> { module });

        var graph = new DependencyGraph(
            new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["N1.A"] = new HashSet<string> { "N2.C" },
                ["N1.B"] = new HashSet<string>(),
                ["N2.C"] = new HashSet<string>()
            },
            new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["N1"] = new HashSet<string> { "N2" },
                ["N2"] = new HashSet<string>()
            });

        var model = new CodeModel(codebase, graph);

        // Use the real calculators via MetricsEngine default ctor
        var engine = new MetricsEngine();

        // Act
        var result = engine.Compute(model);

        // ---- Assertions (high-level consistency) ----

        // Summary
        result.Summary.TotalNamespaces.ShouldBe(2);
        result.Summary.TotalTypes.ShouldBe(3);
        result.Summary.MeanTypesPerNamespace.ShouldBe(1.5, 1e-9);

        // SLOC by type: A(5+3=8), B(4), C(3) => total 15
        result.Summary.TotalSloc.ShouldBe(15);

        // Methods total: 2 + 1 + 1 = 4
        result.Summary.TotalMethods.ShouldBe(4);

        // Complexity total (WMC): A (1+1 + 1+0 = 3), B (1), C (1) => 5
        result.Summary.TotalComplexity.ShouldBe(5);

        // Per-entity collections
        result.Types.Count.ShouldBe(3);
        result.Methods.Count.ShouldBe(4);
        result.Namespaces.Count.ShouldBe(2);

        // Namespace coupling: N1 -> N2; N2 has no outgoing
        var nsByName = result.NamespaceCoupling.ToDictionary(x => x.Namespace);
        nsByName["N1"].Ce.ShouldBe(1);
        nsByName["N1"].Ca.ShouldBe(0);
        nsByName["N1"].Instability.ShouldBe(1.0, 1e-12);
        nsByName["N1"].Abstractness.ShouldBe(0.0, 1e-12);
        nsByName["N1"].NormalizedDistance.ShouldBe(0.0, 1e-12);

        nsByName["N2"].Ce.ShouldBe(0);
        nsByName["N2"].Ca.ShouldBe(1);
        nsByName["N2"].Instability.ShouldBe(0.0, 1e-12);
        nsByName["N2"].Abstractness.ShouldBe(0.0, 1e-12);
        nsByName["N2"].NormalizedDistance.ShouldBe(1.0, 1e-12);

        // Type coupling: A depends on C (internal) + System.String (external)
        var typeByName = result.TypeCoupling.ToDictionary(x => x.TypeFullName);
        typeByName["N1.A"].Dependencies.ShouldBe(2);
        typeByName["N1.A"].InternalDependencies.ShouldBe(1);
        typeByName["N1.A"].FanOut.ShouldBe(1);
        typeByName["N1.A"].FanIn.ShouldBe(0);

        typeByName["N2.C"].FanIn.ShouldBe(1); // A -> C

        // Dependencies (solution-level)
        result.Dependencies.TotalDependencies.ShouldBe(2);   // A->C + A->System.String
        result.Dependencies.InternalDependencies.ShouldBe(1); // A->C
        result.Dependencies.Cycles.Count.ShouldBe(0);
    }
}
