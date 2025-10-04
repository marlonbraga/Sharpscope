using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Xunit;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn;

public sealed class SyntaxUtilsTests
{
    [Fact(DisplayName = "GetDeclaredNamespace works for block namespace")]
    public void GetDeclaredNamespace_Block()
    {
        var code = @"namespace A.B { class C{} }";
        var root = CSharpSyntaxTree.ParseText(code).GetRoot();
        var type = root.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        SyntaxUtils.GetDeclaredNamespace(type).ShouldBe("A.B");
    }

    [Fact(DisplayName = "GetDeclaredNamespace works for file-scoped namespace")]
    public void GetDeclaredNamespace_FileScoped()
    {
        var code = @"namespace A.B; class C { }";
        var root = CSharpSyntaxTree.ParseText(code).GetRoot();
        var type = root.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        SyntaxUtils.GetDeclaredNamespace(type).ShouldBe("A.B");
    }
}
