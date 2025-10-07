using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Sharpscope.Domain.Models;

// Alias to disambiguate TypeKind between Roslyn and Domain
using DomainTypeKind = Sharpscope.Domain.Models.TypeKind;

namespace Sharpscope.Adapters.CSharp.Roslyn.Modeling;

/// <summary>
/// Builds a language-agnostic <see cref="CodeModel"/> from a Roslyn <see cref="Compilation"/>.
/// Every semantic query uses the SemanticModel that belongs to the node's SyntaxTree,
/// preventing "Syntax node is not within syntax tree" exceptions.
/// </summary>
public sealed class CSharpModelBuilder
{
    /// <summary>
    /// Converts a Roslyn <see cref="Compilation"/> to the domain <see cref="CodeModel"/>.
    /// </summary>
    public CodeModel Build(Compilation compilation, CancellationToken ct = default)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));

        var types = CollectTypes(compilation, ct);

        var namespaces = types
            .GroupBy(t => ExtractNamespace(t.FullName), StringComparer.Ordinal)
            .Select(g => new NamespaceNode(g.Key, g.ToList()))
            .ToList();

        var moduleName = string.IsNullOrWhiteSpace(compilation.AssemblyName) ? "Workspace" : compilation.AssemblyName!;
        var module = new ModuleNode(moduleName, namespaces);
        var codebase = new Codebase(new List<ModuleNode> { module });

        var typeEdges = BuildTypeEdges(types);
        var nsEdges = BuildNamespaceEdges(namespaces, typeEdges);

        var graph = new DependencyGraph(typeEdges, nsEdges);
        return new CodeModel(codebase, graph);
    }

    #region Collect types

    private static List<TypeNode> CollectTypes(Compilation compilation, CancellationToken ct)
    {
        var acc = new Dictionary<string, TypeNode>(StringComparer.Ordinal);

        foreach (var tree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();

            var sm = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot(ct);

            foreach (var decl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                var tsym = sm.GetDeclaredSymbol(decl, ct) as INamedTypeSymbol;
                if (tsym is null || tsym.IsImplicitlyDeclared) continue;

                var full = GetTypeFullName(tsym);
                if (acc.ContainsKey(full)) continue; // first declaration wins (partials)

                var node = BuildTypeNode(tsym, compilation, ct);
                acc[full] = node;
            }
        }

        return acc.Values.ToList();
    }

    private static TypeNode BuildTypeNode(INamedTypeSymbol tsym, Compilation compilation, CancellationToken ct)
    {
        var fullName = GetTypeFullName(tsym);
        var kind = MapTypeKind(tsym);
        var isAbstract = tsym.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface || tsym.IsAbstract;

        // Fields (from symbols only) - match FieldNode(Name, TypeName, IsPublic)
        var fields = tsym.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsImplicitlyDeclared)
            .Select(f => new FieldNode(
                Name: f.Name,
                TypeName: NormalizeTypeName(f.Type),
                IsPublic: f.DeclaredAccessibility == Accessibility.Public))
            .ToList();

        // Methods with basic metrics (SLOC/NBD/CALLS/AccessedFields)
        var methods = BuildMethods(tsym, compilation, ct);

        // Dependencies (symbols + syntax, safe across trees)
        var deps = ComputeTypeDependencies(tsym, compilation, ct);

        return new TypeNode(
            FullName: fullName,
            Kind: kind,
            IsAbstract: isAbstract,
            Fields: fields,
            Methods: methods,
            DependsOnTypes: deps.ToList()
        );
    }

    #endregion

    #region Methods

    private static List<MethodNode> BuildMethods(INamedTypeSymbol tsym, Compilation compilation, CancellationToken ct)
    {
        var list = new List<MethodNode>();

        foreach (var msym in tsym.GetMembers().OfType<IMethodSymbol>())
        {
            ct.ThrowIfCancellationRequested();

            if (msym.IsImplicitlyDeclared) continue;
            if (msym.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor)) continue;

            var declRef = msym.DeclaringSyntaxReferences.FirstOrDefault();
            if (declRef is null)
            {
                list.Add(new MethodNode(
                    FullName: GetMethodFullName(msym),
                    Parameters: msym.Parameters.Length,
                    Sloc: 0,
                    DecisionPoints: 0,
                    MaxNestingDepth: 0,
                    Calls: 0,
                    IsPublic: msym.DeclaredAccessibility == Accessibility.Public,
                    AccessedFields: Array.Empty<string>()
                ));
                continue;
            }

            var syntax = declRef.GetSyntax(ct);

            // Metrics
            var sloc = SlocCounter.Count(syntax.ToFullString());
            var nbd = NestingDepthWalker.Compute(syntax);
            var calls = InvocationWalker.Compute(syntax);

            // Always get the model for the syntax's tree
            var smForNode = compilation.GetSemanticModel(syntax.SyntaxTree);
            var accessed = FieldAccessWalker.Compute(syntax, smForNode, tsym);

            list.Add(new MethodNode(
                FullName: GetMethodFullName(msym),
                Parameters: msym.Parameters.Length,
                Sloc: sloc,
                DecisionPoints: 0, // plug CyclomaticComplexityWalker if/when available
                MaxNestingDepth: nbd,
                Calls: calls,
                IsPublic: msym.DeclaredAccessibility == Accessibility.Public,
                AccessedFields: accessed.ToArray()
            ));
        }

        return list;
    }

    #endregion

    #region Dependencies (safe across trees)

    private static HashSet<string> ComputeTypeDependencies(INamedTypeSymbol tsym, Compilation compilation, CancellationToken ct)
    {
        var deps = new HashSet<string>(StringComparer.Ordinal);

        // Symbol-based: base type + interfaces
        if (tsym.BaseType is INamedTypeSymbol bt && bt.SpecialType != SpecialType.System_Object)
            deps.Add(NormalizeTypeName(bt));
        foreach (var iface in tsym.Interfaces)
            deps.Add(NormalizeTypeName(iface));

        // Symbol-based: field/property/method signatures
        foreach (var f in tsym.GetMembers().OfType<IFieldSymbol>())
            deps.Add(NormalizeTypeName(f.Type));
        foreach (var p in tsym.GetMembers().OfType<IPropertySymbol>())
            deps.Add(NormalizeTypeName(p.Type));
        foreach (var m in tsym.GetMembers().OfType<IMethodSymbol>())
        {
            if (m.IsImplicitlyDeclared) continue;
            deps.Add(NormalizeTypeName(m.ReturnType));
            foreach (var prm in m.Parameters)
                deps.Add(NormalizeTypeName(prm.Type));
        }

        // Syntax-based across all partial declarations
        foreach (var sr in tsym.DeclaringSyntaxReferences)
        {
            ct.ThrowIfCancellationRequested();

            if (sr.GetSyntax(ct) is not BaseTypeDeclarationSyntax decl) continue;

            // Object creations => constructor symbol => containing type
            foreach (var newExpr in decl.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var sm = compilation.GetSemanticModel(newExpr.SyntaxTree);
                var ctor = sm.GetSymbolInfo(newExpr, ct).Symbol as IMethodSymbol;
                var target = ctor?.ContainingType;
                if (target is INamedTypeSymbol nt)
                    deps.Add(NormalizeTypeName(nt));
            }

            // Explicit type references in syntax
            foreach (var typeSyntax in decl.DescendantNodes().OfType<TypeSyntax>())
            {
                var sm = compilation.GetSemanticModel(typeSyntax.SyntaxTree);
                var typeInfo = sm.GetTypeInfo(typeSyntax, ct).Type as INamedTypeSymbol;
                if (typeInfo is not null)
                    deps.Add(NormalizeTypeName(typeInfo));
            }
        }

        deps.Remove(GetTypeFullName(tsym));
        deps.RemoveWhere(string.IsNullOrWhiteSpace);
        return deps;
    }

    #endregion

    #region Graphs

    private static Dictionary<string, IReadOnlyCollection<string>> BuildTypeEdges(IEnumerable<TypeNode> types)
    {
        var typeNames = new HashSet<string>(types.Select(t => t.FullName), StringComparer.Ordinal);
        var result = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

        foreach (var t in types)
        {
            var outs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in t.DependsOnTypes ?? Enumerable.Empty<string>())
            {
                if (typeNames.Contains(d) && !string.Equals(d, t.FullName, StringComparison.Ordinal))
                    outs.Add(d);
            }
            result[t.FullName] = outs;
        }

        return result;
    }

    private static Dictionary<string, IReadOnlyCollection<string>> BuildNamespaceEdges(
        IEnumerable<NamespaceNode> namespaces,
        Dictionary<string, IReadOnlyCollection<string>> typeEdges)
    {
        var nsByType = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ns in namespaces)
            foreach (var t in ns.Types)
                nsByType[t.FullName] = ns.Name;

        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var ns in namespaces)
            result[ns.Name] = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (srcType, targets) in typeEdges)
        {
            var srcNs = nsByType[srcType];
            foreach (var tgt in targets)
            {
                var tgtNs = nsByType[tgt];
                if (!string.Equals(srcNs, tgtNs, StringComparison.Ordinal))
                    result[srcNs].Add(tgtNs);
            }
        }

        return result.ToDictionary(kv => kv.Key, kv => (IReadOnlyCollection<string>)kv.Value, StringComparer.Ordinal);
    }

    #endregion

    #region Helpers

    private static string GetTypeFullName(INamedTypeSymbol t)
    {
        var s = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return s.StartsWith("global::", StringComparison.Ordinal) ? s.Substring("global::".Length) : s;
    }

    private static string GetMethodFullName(IMethodSymbol m)
        => m.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    private static string NormalizeTypeName(ITypeSymbol? t)
    {
        if (t is null) return string.Empty;
        if (t is INamedTypeSymbol nt)
        {
            var s = nt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return s.StartsWith("global::", StringComparison.Ordinal) ? s.Substring("global::".Length) : s;
        }
        return t.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private static DomainTypeKind MapTypeKind(INamedTypeSymbol t) =>
        t.TypeKind switch
        {
            Microsoft.CodeAnalysis.TypeKind.Interface => DomainTypeKind.Interface,
            Microsoft.CodeAnalysis.TypeKind.Struct => DomainTypeKind.Struct,
            Microsoft.CodeAnalysis.TypeKind.Enum => DomainTypeKind.Enum,
            Microsoft.CodeAnalysis.TypeKind.Delegate => DomainTypeKind.Delegate,
            _ => DomainTypeKind.Class
        };

    private static string ExtractNamespace(string fullName)
    {
        var idx = fullName.LastIndexOf('.');
        return idx <= 0 ? string.Empty : fullName.Substring(0, idx);
    }

    #endregion
}
