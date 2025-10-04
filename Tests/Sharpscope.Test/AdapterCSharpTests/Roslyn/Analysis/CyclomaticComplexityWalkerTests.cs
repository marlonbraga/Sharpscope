using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Xunit;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn.Analysis;

public sealed class CyclomaticComplexityWalkerTests
{
    [Fact(DisplayName = "If with boolean operators counts decisions (if + && + ||)")]
    public void Cyclo_If_With_And_Or()
    {
        var code = @"
class C {
    bool A, B, Cc;
    void M() {
        if (A && B || Cc) { }
    }
}";
        var method = GetFirstMethod(code);
        var cc = CyclomaticComplexityWalker.Compute(method);

        // Base 1 + if(1) + &&(1) + ||(1) = 4
        cc.ShouldBe(4);
    }

    [Fact(DisplayName = "Counts loops, catch clauses, ?:, ??, switch statement and switch expression")]
    public void Cyclo_Mixed_Constructs()
    {
        var code = @"
class C {
    int F;
    int M1(int x, string? s) {
        for (int i=0;i<1;i++) { }
        foreach (var y in new int[]{1}) { }
        while (x > 0) { x--; }
        do { x++; } while (x < 10);
        try { } catch { } catch (System.Exception) { }
        var z = s ?? ""def"";
        var w = x > 0 ? 1 : 2;
        switch (x) { case 0: break; case 1: break; default: break; }
        var e = x switch { 0 => 1, 1 => 2, _ => 3 };
        return 42;
    }
}";
        var method = GetFirstMethod(code);
        var cc = CyclomaticComplexityWalker.Compute(method);

        // Base 1
        // for(1) foreach(1) while(1) do(1) => +4
        // catch x2 => +2
        // ?? => +1
        // ?: => +1
        // switch statement: 2 non-default cases => +2
        // switch expression: 3 arms => +3
        // Total = 1 + 4 + 2 + 1 + 1 + 2 + 3 = 14
        cc.ShouldBe(14);
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
