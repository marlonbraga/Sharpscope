using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Models;

namespace Sharpscope.Test.DomainTests;

public sealed class CouplingMetricsCalculatorTests
{
    /// <summary>
    /// Builds a small in-memory model:
    /// Namespaces:
    ///   N1: A, B
    ///   N2: C
    /// Type deps:
    ///   A -> B (internal)
    ///   A -> System.String (external)
    ///   B -> C (internal)
    /// Namespace deps:
    ///   N1 -> N2 (because B depends on C)
    /// Expected:
    ///   - Namespace N1: CA=0, CE=1, I=1, A=0, D=0
    ///   - Namespace N2: CA=1, CE=0, I=0, A=0, D=1
    ///   - Type A: DEP=2 (B + external), I-DEP=1, FAN-OUT=1, FAN-IN=0
    ///   - Type B: DEP=1 (C), I-DEP=1, FAN-OUT=1, FAN-IN=1 (from A)
    ///   - Type C: DEP=0, I-DEP=0, FAN-OUT=0, FAN-IN=1 (from B)
    /// </summary>
    [Fact]
    public void Coupling_Is_Computed_As_Expected_In_Simple_Model()
    {
        // Build types
        var typeA = new TypeNode(
            FullName: "N1.A",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode>(),
            Methods: new List<MethodNode>(),
            DependsOnTypes: new List<string> { "N1.B", "System.String" } // one internal, one external
        );

        var typeB = new TypeNode(
            FullName: "N1.B",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode>(),
            Methods: new List<MethodNode>(),
            DependsOnTypes: new List<string> { "N2.C" } // internal
        );

        var typeC = new TypeNode(
            FullName: "N2.C",
            Kind: TypeKind.Class,
            IsAbstract: false,
            Fields: new List<FieldNode>(),
            Methods: new List<MethodNode>(),
            DependsOnTypes: new List<string>() // none
        );

        // Namespaces
        var ns1 = new NamespaceNode("N1", new List<TypeNode> { typeA, typeB });
        var ns2 = new NamespaceNode("N2", new List<TypeNode> { typeC });

        // Module
        var module = new ModuleNode("M1", new List<NamespaceNode> { ns1, ns2 });

        // Codebase
        var codebase = new Codebase(new List<ModuleNode> { module });

        // Dependency graphs (internal only)
        var typeEdges = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["N1.A"] = new HashSet<string> { "N1.B" },
            ["N1.B"] = new HashSet<string> { "N2.C" },
            ["N2.C"] = new HashSet<string>() // explicit empty set
        };

        var nsEdges = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["N1"] = new HashSet<string> { "N2" },
            ["N2"] = new HashSet<string>()
        };

        var graph = new DependencyGraph(typeEdges, nsEdges);
        var model = new CodeModel(codebase, graph);

        var calc = new CouplingMetricsCalculator();

        // --- Type coupling ---
        var typeCoupling = calc.ComputeTypeCoupling(model).ToDictionary(x => x.TypeFullName);

        Assert.True(typeCoupling.ContainsKey("N1.A"));
        Assert.True(typeCoupling.ContainsKey("N1.B"));
        Assert.True(typeCoupling.ContainsKey("N2.C"));

        var a = typeCoupling["N1.A"];
        Assert.Equal(2, a.Dependencies);          // B + external
        Assert.Equal(1, a.InternalDependencies);  // B
        Assert.Equal(1, a.FanOut);                // B
        Assert.Equal(0, a.FanIn);                 // no one depends on A

        var b = typeCoupling["N1.B"];
        Assert.Equal(1, b.Dependencies);          // C
        Assert.Equal(1, b.InternalDependencies);  // C
        Assert.Equal(1, b.FanOut);                // C
        Assert.Equal(1, b.FanIn);                 // A -> B

        var c = typeCoupling["N2.C"];
        Assert.Equal(0, c.Dependencies);
        Assert.Equal(0, c.InternalDependencies);
        Assert.Equal(0, c.FanOut);
        Assert.Equal(1, c.FanIn);                 // B -> C

        // --- Namespace coupling ---
        var nsCoupling = calc.ComputeNamespaceCoupling(model).ToDictionary(x => x.Namespace);

        Assert.True(nsCoupling.ContainsKey("N1"));
        Assert.True(nsCoupling.ContainsKey("N2"));

        var n1 = nsCoupling["N1"];
        Assert.Equal(0, n1.Ca);
        Assert.Equal(1, n1.Ce);
        Assert.Equal(1.0, n1.Instability, 3);
        Assert.Equal(0.0, n1.Abstractness, 3);
        Assert.Equal(0.0, n1.NormalizedDistance, 3);

        var n2 = nsCoupling["N2"];
        Assert.Equal(1, n2.Ca);
        Assert.Equal(0, n2.Ce);
        Assert.Equal(0.0, n2.Instability, 3);
        Assert.Equal(0.0, n2.Abstractness, 3);
        Assert.Equal(1.0, n2.NormalizedDistance, 3);
    }
}
