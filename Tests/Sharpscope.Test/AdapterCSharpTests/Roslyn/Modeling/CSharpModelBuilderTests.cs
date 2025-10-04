using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Sharpscope.Adapters.CSharp.Roslyn.Modeling;
using Xunit;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn;

public sealed class CSharpModelBuilderTests
{
    [Fact(DisplayName = "Build creates namespaces, types and dependency graph from compilation")]
    public void Build_FromCompilation_Works()
    {
        var code = @"
namespace N1 { 
    public class A { 
        public void M() { var b = new N2.B(); b.GetHashCode(); }
    } 
}
namespace N2 { public class B { } }
";
        var tree = CSharpSyntaxTree.ParseText(code);
        var comp = CSharpCompilation.Create("t",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var builder = new CSharpModelBuilder();
        var model = builder.Build(comp);

        // Namespaces present
        var ns = model.Codebase.Modules.Single().Namespaces;
        ns.Select(n => n.Name).OrderBy(s => s).ShouldBe(new[] { "N1", "N2" });

        // Types present
        var types = ns.SelectMany(n => n.Types).ToList();
        types.Any(t => t.FullName == "N1.A").ShouldBeTrue();
        types.Any(t => t.FullName == "N2.B").ShouldBeTrue();

        // Dependency edges (A -> B) and namespace edge (N1 -> N2)
        model.DependencyGraph.TypeEdges.ContainsKey("N1.A").ShouldBeTrue();
        model.DependencyGraph.TypeEdges["N1.A"].ShouldContain("N2.B");
        model.DependencyGraph.NamespaceEdges["N1"].ShouldContain("N2");
    }
}
