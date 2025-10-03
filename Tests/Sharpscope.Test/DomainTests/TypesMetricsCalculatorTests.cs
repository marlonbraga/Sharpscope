using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.DomainTests;

public sealed class TypesMetricsCalculatorTests
{
    [Fact(DisplayName = "ComputeFor returns all metrics for a single type")]
    public void ComputeFor_SingleType_Works()
    {
        // Arrange:
        // Type T1 depends on T2 (internal) and System.String (external).
        // Graph: T1 -> T2
        // Methods:
        //   M1: Sloc=10, DP=2 (cyclo 3), public, accesses F1
        //   M2: Sloc=5,  DP=0 (cyclo 1), non-public, accesses F1
        // Fields: F1, F2  (only F1 is accessed; LCOM3 = 1 - sumμ/(m*n) = 1 - 2/(2*2) = 0.5)
        var m1 = new MethodNode("N1.T1.M1", 0, 10, 2, 1, 0, true, new List<string> { "F1" });
        var m2 = new MethodNode("N1.T1.M2", 0, 5, 0, 0, 0, false, new List<string> { "F1" });

        var t1 = new TypeNode(
            FullName: "N1.T1",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode> { new("F1", "int", true), new("F2", "int", false) },
            Methods: new List<MethodNode> { m1, m2 },
            DependsOnTypes: new List<string> { "N1.T2", "System.String" });

        var t2 = new TypeNode(
            FullName: "N1.T2",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode>(),
            Methods: new List<MethodNode>(),
            DependsOnTypes: new List<string>());

        var ns = new NamespaceNode("N1", new List<TypeNode> { t1, t2 });
        var module = new ModuleNode("M1", new List<NamespaceNode> { ns });
        var codebase = new Codebase(new List<ModuleNode> { module });

        var typeEdges = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["N1.T1"] = new HashSet<string> { "N1.T2" },
            ["N1.T2"] = new HashSet<string>()
        };

        var graph = new DependencyGraph(typeEdges, new Dictionary<string, IReadOnlyCollection<string>>());
        var model = new CodeModel(codebase, graph);

        var calc = new TypesMetricsCalculator();

        // Act
        var tm = calc.ComputeFor(t1, model);

        // Assert
        tm.TypeFullName.ShouldBe("N1.T1");
        tm.Sloc.ShouldBe(15);                // 10 + 5
        tm.Nom.ShouldBe(2);
        tm.Npm.ShouldBe(1);
        tm.Wmc.ShouldBe(4);                  // (1+2) + (1+0)
        tm.Dep.ShouldBe(2);                  // T2 + System.String
        tm.IDep.ShouldBe(1);                 // internal out to T2
        tm.FanOut.ShouldBe(1);
        tm.FanIn.ShouldBe(0);                // nobody depends on T1
        tm.Noa.ShouldBe(2);                  // F1, F2
        tm.Lcom3.ShouldBe(0.5, 0.000001);    // bounded LCOM3
    }

    [Fact(DisplayName = "ComputeAll returns metrics for all types in a model")]
    public void ComputeAll_ModelWithTwoTypes_Works()
    {
        // Arrange:
        // T1 -> T2 (internal). T2 has no deps.
        // Provide distinct method/field patterns to get different metrics.
        var m1 = new MethodNode("N1.T1.M1", 1, 8, 1, 1, 1, true, new List<string> { "F1" });
        var m2 = new MethodNode("N1.T1.M2", 0, 2, 0, 0, 0, false, new List<string>());

        var t1 = new TypeNode(
            FullName: "N1.T1",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode> { new("F1", "int", true) },
            Methods: new List<MethodNode> { m1, m2 },
            DependsOnTypes: new List<string> { "N1.T2" });

        var t2 = new TypeNode(
            FullName: "N1.T2",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode> { new("G", "int", false) },
            Methods: new List<MethodNode> { new("N1.T2.N", 0, 3, 0, 0, 0, true, new List<string> { "G" }) },
            DependsOnTypes: new List<string>());

        var ns = new NamespaceNode("N1", new List<TypeNode> { t1, t2 });
        var module = new ModuleNode("M1", new List<NamespaceNode> { ns });
        var codebase = new Codebase(new List<ModuleNode> { module });

        var typeEdges = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["N1.T1"] = new HashSet<string> { "N1.T2" },
            ["N1.T2"] = new HashSet<string>()
        };

        var graph = new DependencyGraph(typeEdges, new Dictionary<string, IReadOnlyCollection<string>>());
        var model = new CodeModel(codebase, graph);

        var calc = new TypesMetricsCalculator();

        // Act
        var list = calc.ComputeAll(model);
        var byName = list.ToDictionary(x => x.TypeFullName);

        // Assert T1
        var tm1 = byName["N1.T1"];
        tm1.Sloc.ShouldBe(10);              // 8 + 2
        tm1.Nom.ShouldBe(2);
        tm1.Npm.ShouldBe(1);
        tm1.Wmc.ShouldBe(3);                // (1+1) + (1+0)
        tm1.Dep.ShouldBe(1);                // only T2
        tm1.IDep.ShouldBe(1);
        tm1.FanOut.ShouldBe(1);
        tm1.FanIn.ShouldBe(0);
        tm1.Noa.ShouldBe(1);
        // LCOM3: fields=1 (n==1) => defined as 0.0
        tm1.Lcom3.ShouldBe(0.0, 0.000001);

        // Assert T2
        var tm2 = byName["N1.T2"];
        tm2.Sloc.ShouldBe(3);
        tm2.Nom.ShouldBe(1);
        tm2.Npm.ShouldBe(1);
        tm2.Wmc.ShouldBe(1);                // (1+0)
        tm2.Dep.ShouldBe(0);
        tm2.IDep.ShouldBe(0);
        tm2.FanOut.ShouldBe(0);
        tm2.FanIn.ShouldBe(1);              // T1 -> T2
        tm2.Noa.ShouldBe(1);
        // LCOM3: m=1, n=1 -> 0.0 by definition
        tm2.Lcom3.ShouldBe(0.0, 0.000001);
    }
}
