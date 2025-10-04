using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Xunit;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn.Analysis;

public sealed class InvocationWalkerTests
{
    [Fact(DisplayName = "Counts simple, member and fluent invocations")]
    public void Compute_InvocationCounts_Works()
    {
        var code = @"
static class E { public static string Ex(this string s) => s; }
class C {
    void M() {
        Foo();
        this.Bar();
        System.Console.WriteLine(""x"");
        ""a"".Ex().Ex();
        // new C() is object creation, not an invocation:
        var c = new C();
    }
    void Foo() {}
    void Bar() {}
}";
        var method = GetMethod(code, "M");
        var count = InvocationWalker.Compute(method);

        // Foo() -> 1
        // this.Bar() -> 1
        // Console.WriteLine -> 1
        // "a".Ex().Ex() -> 2 (fluent)
        // TOTAL: 5
        count.ShouldBe(5);
    }

    #region Helpers

    private static MethodDeclarationSyntax GetMethod(string code, string name)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        return root.DescendantNodes()
                   .OfType<MethodDeclarationSyntax>()
                   .First(m => m.Identifier.Text == name);
    }

    #endregion
}
