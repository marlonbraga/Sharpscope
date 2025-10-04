using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Xunit;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn.Analysis;

public sealed class NestingDepthWalkerTests
{
    [Fact(DisplayName = "Nested control structures increment depth")]
    public void Compute_NestedBlocks_Works()
    {
        var code = @"
class C {
    void M(bool a, bool b, int n) {
        if (a)
            while (b)
            {
                for (int i=0;i<n;i++)
                {
                    if (i % 2 == 0) { }
                }
            }
    }
}";
        var method = GetFirstMethod(code);
        var nbd = NestingDepthWalker.Compute(method);

        // if (1) -> while (2) -> for (3) -> if (4) => 4
        nbd.ShouldBe(4);
    }

    [Fact(DisplayName = "Else branch also contributes to depth")]
    public void Compute_ElseBranch_Works()
    {
        var code = @"
class C {
    void M(bool a) {
        if (a) { }
        else if (!a) { if(a) { } }
        else { }
    }
}";
        var method = GetFirstMethod(code);
        var nbd = NestingDepthWalker.Compute(method);

        // if (1)
        // else-if (enters else, then if inside): else(2) -> inner if (3)
        // Max depth should be 3
        nbd.ShouldBe(3);
    }

    #region Helpers

    private static MethodDeclarationSyntax GetFirstMethod(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        return root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    #endregion
}
