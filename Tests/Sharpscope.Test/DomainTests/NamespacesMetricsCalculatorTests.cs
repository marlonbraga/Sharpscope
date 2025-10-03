using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.DomainTests;

public sealed class NamespacesMetricsCalculatorTests
{
    [Fact(DisplayName = "ComputeFor returns NOC and NAC for a single namespace")]
    public void ComputeFor_SingleNamespace_Works()
    {
        // Arrange: namespace with 3 types:
        // - abstract class (counts in NAC)
        // - interface (does not count in NAC)
        // - concrete class (does not count in NAC)
        var abstractClass = new TypeNode(
            FullName: "N1.AbstractThing",
            Kind: TypeKind.Class,
            IsAbstract: true,
            Fields: new List<FieldNode>(),
            Methods: new List<MethodNode>(),
            DependsOnTypes: new List<string>());

        var iface = new TypeNode(
            FullName: "N1.IThing",
            Kind: TypeKind.Interface,
            IsAbstract: true, // ignored for NAC by design
            Fields: new List<FieldNode>(),
            Methods: new List<MethodNode>(),
            DependsOnTypes: new List<string>());

        var concreteClass = new TypeNode(
            FullName: "N1.Concrete",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode>(),
            Methods: new List<MethodNode>(),
            DependsOnTypes: new List<string>());

        var ns = new NamespaceNode("N1", new List<TypeNode> { abstractClass, iface, concreteClass });

        var calc = new NamespacesMetricsCalculator();

        // Act
        var metrics = calc.ComputeFor(ns);

        // Assert
        metrics.Namespace.ShouldBe("N1");
        metrics.Noc.ShouldBe(3);
        metrics.Nac.ShouldBe(1); // only the abstract class
    }

    [Fact(DisplayName = "ComputeAll returns metrics for each namespace in the model")]
    public void ComputeAll_ModelWithMultipleNamespaces_Works()
    {
        // Arrange
        var n1 = new NamespaceNode("N1", new List<TypeNode>
        {
            new TypeNode("N1.A", TypeKind.Class,   false, new List<FieldNode>(), new List<MethodNode>(), new List<string>()),
            new TypeNode("N1.B", TypeKind.Record,  true,  new List<FieldNode>(), new List<MethodNode>(), new List<string>()), // abstract record
            new TypeNode("N1.I", TypeKind.Interface, true, new List<FieldNode>(), new List<MethodNode>(), new List<string>())  // interface (ignored)
        });

        var n2 = new NamespaceNode("N2", new List<TypeNode>
        {
            new TypeNode("N2.S", TypeKind.Struct,  false, new List<FieldNode>(), new List<MethodNode>(), new List<string>()),
            new TypeNode("N2.E", TypeKind.Enum,    false, new List<FieldNode>(), new List<MethodNode>(), new List<string>())
        });

        var module = new ModuleNode("M1", new List<NamespaceNode> { n1, n2 });
        var codebase = new Codebase(new List<ModuleNode> { module });
        var model = new CodeModel(codebase, DependencyGraph.Empty);

        var calc = new NamespacesMetricsCalculator();

        // Act
        var list = calc.ComputeAll(model);
        var byNs = list.ToDictionary(x => x.Namespace);

        // Assert
        byNs.Keys.ShouldBe(new[] { "N1", "N2" }, ignoreOrder: true);

        byNs["N1"].ShouldSatisfyAllConditions(
            x => x.Noc.ShouldBe(3),
            x => x.Nac.ShouldBe(1) // only the abstract record counts
        );

        byNs["N2"].ShouldSatisfyAllConditions(
            x => x.Noc.ShouldBe(2),
            x => x.Nac.ShouldBe(0)
        );
    }
}
