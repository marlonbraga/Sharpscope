using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Sharpscope.Domain.Models;

namespace Sharpscope.Adapters.CSharp.Roslyn.Modeling;

public sealed class CSharpModelBuilder
{
    public CodeModel Build(Compilation compilation)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));

        var types = CollectTypes(compilation);
        var namespaces = GroupByNamespace(types);
        var module = new ModuleNode("Main", namespaces);
        var codebase = new Codebase(new List<ModuleNode> { module });

        var graph = DependencyGraphBuilder.Build(types);
        return new CodeModel(codebase, graph);
    }

    private static List<TypeNode> CollectTypes(Compilation compilation)
    {
        var list = new List<TypeNode>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetRoot();

            foreach (var tdecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var tsym = model.GetDeclaredSymbol(tdecl) as INamedTypeSymbol;
                if (tsym is null) continue;

                var fullName = tsym.GetFullName();
                var kind = tsym.ToDomainTypeKind();
                var isAbs = tsym.IsAbstractType();

                var fields = tsym.GetMembers().OfType<IFieldSymbol>()
                                 .Where(f => !f.IsImplicitlyDeclared)
                                 .Select(f => new FieldNode(
                                     f.Name,
                                     f.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                                     f.DeclaredAccessibility == Accessibility.Public))
                                 .ToList();

                var methods = BuildMethods(tsym, model);
                var deps = ComputeTypeDependencies(tsym, model);

                list.Add(new TypeNode(
                    FullName: fullName,
                    Kind: kind,
                    IsAbstract: isAbs,
                    Fields: fields,
                    Methods: methods,
                    DependsOnTypes: deps.ToList()
                ));
            }
        }
        return list;
    }

    private static List<MethodNode> BuildMethods(INamedTypeSymbol tsym, SemanticModel model)
    {
        var methods = new List<MethodNode>();

        foreach (var msym in tsym.GetMembers().OfType<IMethodSymbol>())
        {
            if (msym.MethodKind != MethodKind.Ordinary) continue;
            if (msym.IsImplicitlyDeclared) continue;

            var decl = msym.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;

            // Nome completo do método: <TipoFullName>.<MethodName>
            var methodFullName = $"{tsym.GetFullName()}.{msym.Name}";
            var isPublic = msym.DeclaredAccessibility == Accessibility.Public;
            var paramCount = msym.Parameters.Length;

            if (decl is null)
            {
                // Sem declaração (ex.: método parcial em outro arquivo) → métricas zeradas
                methods.Add(new MethodNode(
                    methodFullName,           // FullName
                    paramCount,               // Parameters
                    0,                        // Sloc
                    0,                        // DecisionPoints
                    0,                        // MaxNestingDepth
                    0,                        // Calls
                    isPublic,                 // IsPublic
                    Array.Empty<string>()     // AccessedFields
                ));
                continue;
            }

            // Métricas
            var sloc = SlocCounter.Count(decl.ToFullString());
            var cc = CyclomaticComplexityWalker.Compute(decl); // 1 + decision points
            var dps = Math.Max(0, cc - 1);                      // DecisionPoints
            var nbd = NestingDepthWalker.Compute(decl);
            var calls = InvocationWalker.Compute(decl);
            var fields = FieldAccessWalker.Compute(decl, model, tsym); // nomes de campos do próprio tipo

            methods.Add(new MethodNode(
                methodFullName,             // FullName
                paramCount,                 // Parameters
                sloc,                       // Sloc
                dps,                        // DecisionPoints
                nbd,                        // MaxNestingDepth
                calls,                      // Calls
                isPublic,                   // IsPublic
                fields is List<string> lst ? lst : new List<string>(fields) // AccessedFields
            ));
        }

        return methods;
    }

    private static HashSet<string> ComputeTypeDependencies(INamedTypeSymbol tsym, SemanticModel model)
    {
        var deps = new HashSet<string>(StringComparer.Ordinal);

        foreach (var msym in tsym.GetMembers().OfType<IMethodSymbol>())
        {
            if (msym.MethodKind != MethodKind.Ordinary || msym.IsImplicitlyDeclared) continue;

            var decl = msym.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (decl is null) continue;

            // Invocations → containing type of the target method
            foreach (var inv in decl.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>())
            {
                var callee = model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
                var ct = callee?.ContainingType;
                if (ct is not null && !SymbolEqualityComparer.Default.Equals(ct, tsym))
                    deps.Add(ct.GetFullName());
            }

            // Object creations → created type
            foreach (var newObj in decl.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ObjectCreationExpressionSyntax>())
            {
                var t = model.GetSymbolInfo(newObj.Type).Symbol as ITypeSymbol;
                if (t is not null && !SymbolEqualityComparer.Default.Equals(t, tsym))
                    deps.Add(t.GetFullName());
            }

            // Member access to fields/properties of other types
            foreach (var ma in decl.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax>())
            {
                var sym = model.GetSymbolInfo(ma).Symbol;
                var ct = (sym as IFieldSymbol)?.ContainingType ?? (sym as IPropertySymbol)?.ContainingType ?? (sym as IMethodSymbol)?.ContainingType;
                if (ct is not null && !SymbolEqualityComparer.Default.Equals(ct, tsym))
                    deps.Add(ct.GetFullName());
            }
        }

        return deps;
    }

    private static List<NamespaceNode> GroupByNamespace(List<TypeNode> types)
    {
        var groups = types.GroupBy(t => NamespaceOf(t.FullName), StringComparer.Ordinal);
        var list = new List<NamespaceNode>();
        foreach (var g in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            list.Add(new NamespaceNode(g.Key, g.ToList()));
        }
        return list;
    }

    private static string NamespaceOf(string fullTypeName)
    {
        var idx = fullTypeName.LastIndexOf('.');
        return idx <= 0 ? "" : fullTypeName.Substring(0, idx);
    }
}
