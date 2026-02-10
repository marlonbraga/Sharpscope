using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Models;
using Sharpscope.Test.Helpers;
using Xunit;

namespace Sharpscope.Test.DomainTests;

public sealed class MetricsRegressionTests
{
    [Fact(DisplayName = "Metrics from CodeGraph match legacy CodeModel results")]
    public void Metrics_FromGraph_MatchLegacyModel()
    {
        var model = BuildModel();
        var graph = TestGraphFactory.FromCodeModel(model);

        var methods = new MethodsMetricsCalculator().ComputeAll(model);
        var types = new TypesMetricsCalculator().ComputeAll(model);
        var namespaces = new NamespacesMetricsCalculator().ComputeAll(model);
        var coupling = new CouplingMetricsCalculator();
        var nsCoupling = coupling.ComputeNamespaceCoupling(model);
        var typeCoupling = coupling.ComputeTypeCoupling(model);
        var dependencies = new DependenciesMetricsCalculator().Compute(model);
        var summary = new SummaryMetricsAggregator().Compute(model, types, methods);

        var snapshot = new MetricsEngine().Compute(graph);

        snapshot.Summary.ShouldBe(summary);

        var methodsByName = snapshot.Methods.Values.ToDictionary(m => m.MethodFullName);
        foreach (var m in methods)
            methodsByName[m.MethodFullName].ShouldBe(m);

        var typesByName = snapshot.Types.Values.ToDictionary(t => t.TypeFullName);
        foreach (var t in types)
        {
            var actual = typesByName[t.TypeFullName];
            actual.Sloc.ShouldBe(t.Sloc);
            actual.Nom.ShouldBe(t.Nom);
            actual.Npm.ShouldBe(t.Npm);
            actual.Wmc.ShouldBe(t.Wmc);
            actual.Dep.ShouldBe(t.Dep);
            actual.IDep.ShouldBe(t.IDep);
            actual.FanIn.ShouldBe(t.FanIn);
            actual.FanOut.ShouldBe(t.FanOut);
            actual.Noa.ShouldBe(t.Noa);
            actual.Lcom3.ShouldBe(t.Lcom3, 1e-9);
        }

        var namespacesByName = snapshot.Namespaces.Values.ToDictionary(n => n.Namespace);
        foreach (var n in namespaces)
            namespacesByName[n.Namespace].ShouldBe(n);

        var nsCouplingByName = snapshot.NamespaceCoupling.Values.ToDictionary(n => n.Namespace);
        foreach (var n in nsCoupling)
            nsCouplingByName[n.Namespace].ShouldBe(n);

        var typeCouplingByName = snapshot.TypeCoupling.Values.ToDictionary(t => t.TypeFullName);
        foreach (var t in typeCoupling)
            typeCouplingByName[t.TypeFullName].ShouldBe(t);

        snapshot.Dependencies.TotalDependencies.ShouldBe(dependencies.TotalDependencies);
        snapshot.Dependencies.InternalDependencies.ShouldBe(dependencies.InternalDependencies);
        snapshot.Dependencies.Cycles.Count.ShouldBe(dependencies.Cycles.Count);
    }

    private static CodeModel BuildModel()
    {
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

        return new CodeModel(codebase, graph);
    }
}
