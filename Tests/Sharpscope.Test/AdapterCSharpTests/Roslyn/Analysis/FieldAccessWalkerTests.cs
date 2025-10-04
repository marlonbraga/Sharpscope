using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Xunit;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn.Analysis;

public sealed class FieldAccessWalkerTests
{
    [Fact(DisplayName = "Collects fields of the owning type (instance and static) and ignores locals/other types")]
    public void Compute_FieldNames_Works()
    {
        var code = @"
public class Other { public static int S; }

public class C {
    private int f1;
    private int f2;
    private static int S;

    public void M()
    {
        f1 = 1;
        var x = f2 + this.f1;
        int f1_local = 0;
        f1_local++;

        // static field of this type
        S++;

        // field of another type - must not count
        var y = Other.S;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("t",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var names = FieldAccessWalker.Compute(method, model);

        names.OrderBy(s => s, StringComparer.Ordinal)
             .ToArray()
             .ShouldBe(new[] { "S", "f1", "f2" });
    }
}
