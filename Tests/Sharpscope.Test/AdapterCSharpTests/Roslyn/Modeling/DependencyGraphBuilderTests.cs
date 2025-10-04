using System.Collections.Generic;
using Shouldly;
using Sharpscope.Adapters.CSharp.Roslyn.Modeling;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn;

public sealed class DependencyGraphBuilderTests
{
    [Fact(DisplayName = "Build creates internal type and namespace edges")]
    public void Build_Works()
    {
        var a = new TypeNode("N1.A", TypeKind.Class, false, new List<FieldNode>(), new List<MethodNode>(), new List<string> { "N2.B", "System.String" });
        var b = new TypeNode("N2.B", TypeKind.Class, false, new List<FieldNode>(), new List<MethodNode>(), new List<string>());
        var types = new List<TypeNode> { a, b };

        var g = DependencyGraphBuilder.Build(types);

        g.TypeEdges["N1.A"].ShouldContain("N2.B");
        g.TypeEdges["N2.B"].Count.ShouldBe(0);

        g.NamespaceEdges["N1"].ShouldContain("N2");
        g.NamespaceEdges.ContainsKey("N2").ShouldBeFalse();
    }
}
