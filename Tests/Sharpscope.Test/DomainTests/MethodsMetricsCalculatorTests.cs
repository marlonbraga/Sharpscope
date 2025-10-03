using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.DomainTests;

public sealed class MethodsMetricsCalculatorTests
{
    [Fact(DisplayName = "Compute for a single method returns expected metrics")]
    public void Compute_SingleMethod_Works()
    {
        // Arrange
        var method = new MethodNode(
            FullName: "N1.A.M",
            Parameters: 2,
            Sloc: 7,
            DecisionPoints: 3,   // cyclo = 1 + 3 = 4
            MaxNestingDepth: 2,
            Calls: 5,
            IsPublic: true,
            AccessedFields: new List<string>()
        );

        var calc = new MethodsMetricsCalculator();

        // Act
        var metrics = calc.Compute(method);

        // Assert (Shouldly)
        metrics.MethodFullName.ShouldBe("N1.A.M");
        metrics.Mloc.ShouldBe(7);
        metrics.Cyclo.ShouldBe(4);      // 1 + 3
        metrics.Calls.ShouldBe(5);
        metrics.Nbd.ShouldBe(2);
        metrics.Parameters.ShouldBe(2);
    }

    [Fact(DisplayName = "ComputeAll aggregates metrics for all methods in the model")]
    public void ComputeAll_FromModel_Works()
    {
        // Arrange: build a tiny model with two types and three methods
        var m1 = new MethodNode("N1.A.M1", 1, 10, 0, 1, 2, true, new List<string>());
        var m2 = new MethodNode("N1.A.M2", 0, 4, 2, 0, 0, false, new List<string>());
        var m3 = new MethodNode("N2.B.M3", 3, 12, 5, 3, 1, true, new List<string>());

        var typeA = new TypeNode(
            FullName: "N1.A",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode>(),
            Methods: new List<MethodNode> { m1, m2 },
            DependsOnTypes: new List<string>());

        var typeB = new TypeNode(
            FullName: "N2.B",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode>(),
            Methods: new List<MethodNode> { m3 },
            DependsOnTypes: new List<string>());

        var ns1 = new NamespaceNode("N1", new List<TypeNode> { typeA });
        var ns2 = new NamespaceNode("N2", new List<TypeNode> { typeB });

        var module = new ModuleNode("M1", new List<NamespaceNode> { ns1, ns2 });
        var codebase = new Codebase(new List<ModuleNode> { module });
        var model = new CodeModel(codebase, DependencyGraph.Empty);

        var calc = new MethodsMetricsCalculator();

        // Act
        var list = calc.ComputeAll(model);
        var byName = list.ToDictionary(x => x.MethodFullName);

        // Assert
        list.Count.ShouldBe(3);

        byName["N1.A.M1"].ShouldSatisfyAllConditions(
            x => x.Mloc.ShouldBe(10),
            x => x.Cyclo.ShouldBe(1),     // 1 + decisionPoints(0)
            x => x.Nbd.ShouldBe(1),
            x => x.Calls.ShouldBe(2),
            x => x.Parameters.ShouldBe(1)
        );

        byName["N1.A.M2"].ShouldSatisfyAllConditions(
            x => x.Mloc.ShouldBe(4),
            x => x.Cyclo.ShouldBe(3),     // 1 + 2
            x => x.Nbd.ShouldBe(0),
            x => x.Calls.ShouldBe(0),
            x => x.Parameters.ShouldBe(0)
        );

        byName["N2.B.M3"].ShouldSatisfyAllConditions(
            x => x.Mloc.ShouldBe(12),
            x => x.Cyclo.ShouldBe(6),     // 1 + 5
            x => x.Nbd.ShouldBe(3),
            x => x.Calls.ShouldBe(1),
            x => x.Parameters.ShouldBe(3)
        );
    }
}
