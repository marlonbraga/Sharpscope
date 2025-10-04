using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Xunit;
using DomainTypeKind = Sharpscope.Domain.Models.TypeKind;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn;

public sealed class SymbolExtensionsTests
{
    [Fact(DisplayName = "GetFullName and ToDomainTypeKind")]
    public void FullName_And_Kind()
    {
        var code = @"namespace N1 { public interface I {} public class C {} }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var comp = CSharpCompilation.Create("t",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = comp.GetSemanticModel(tree);
        var root = tree.GetRoot();

        var iDecl = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().First();
        var cDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        var iSym = (INamedTypeSymbol)model.GetDeclaredSymbol(iDecl)!;
        var cSym = (INamedTypeSymbol)model.GetDeclaredSymbol(cDecl)!;

        iSym.GetFullName().ShouldBe("N1.I");
        cSym.GetFullName().ShouldBe("N1.C");

        iSym.ToDomainTypeKind().ShouldBe(DomainTypeKind.Interface);
        cSym.ToDomainTypeKind().ShouldBe(DomainTypeKind.Class);
    }
}
