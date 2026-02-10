using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Sharpscope.Adapters.CSharp.Roslyn.Workspace;
using Sharpscope.Domain.Models;

namespace Sharpscope.Adapters.CSharp.Roslyn.Modeling;

using DomainTypeKind = Sharpscope.Domain.Models.TypeKind;

/// <summary>
/// Builds a canonical <see cref="CodeGraph"/> from Roslyn workspace projects/compilations.
/// </summary>
public sealed class CodeGraphBuilder
{
    public CodeGraph Build(RoslynWorkspaceResult workspace, CancellationToken ct = default)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));

        var nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var edges = new List<GraphEdge>();

        var solutionName = ResolveSolutionName(workspace);
        var solutionId = GraphIdFactory.CreateSolutionId(solutionName);

        nodes[solutionId] = new GraphNode(
            solutionId,
            GraphNodeKind.Solution,
            solutionName,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GraphAttributeKeys.SolutionName] = solutionName
            }
        );

        foreach (var project in workspace.Projects.OrderBy(p => p.ProjectName, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();

            var projectRelativePath = ResolveProjectRelativePath(workspace.RootPath, project.ProjectPath);
            var projectId = GraphIdFactory.CreateProjectId(projectRelativePath);

            nodes[projectId] = new GraphNode(
                projectId,
                GraphNodeKind.Project,
                project.ProjectName,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [GraphAttributeKeys.ProjectName] = project.ProjectName,
                    [GraphAttributeKeys.ProjectRelativePath] = projectRelativePath
                }
            );

            edges.Add(new GraphEdge(
                solutionId,
                projectId,
                GraphEdgeKind.Contains,
                Label: null,
                Attributes: new Dictionary<string, string>(StringComparer.Ordinal),
                Evidence: null,
                Confidence: 1.0
            ));

            BuildProjectGraph(projectId, project.Compilation, nodes, edges, ct);
        }

        var orderedNodes = nodes.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var orderedEdges = edges
            .OrderBy(e => e.FromId, StringComparer.Ordinal)
            .ThenBy(e => e.Kind)
            .ThenBy(e => e.ToId, StringComparer.Ordinal)
            .ThenBy(e => e.Label, StringComparer.Ordinal)
            .ToList();

        return new CodeGraph(orderedNodes, orderedEdges);
    }

    private static void BuildProjectGraph(
        string projectId,
        Compilation compilation,
        IDictionary<string, GraphNode> nodes,
        IList<GraphEdge> edges,
        CancellationToken ct)
    {
        var typeSymbols = CollectDeclaredTypes(compilation, ct);
        var typeInfoBySymbol = new Dictionary<INamedTypeSymbol, TypeBuildInfo>(SymbolEqualityComparer.Default);
        var typeIdByFullName = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var tsym in typeSymbols)
        {
            ct.ThrowIfCancellationRequested();

            var fullName = GetTypeFullName(tsym);
            var metadataName = GetTypeMetadataName(tsym);
            var typeId = GraphIdFactory.CreateTypeId(projectId, metadataName);

            if (typeIdByFullName.ContainsKey(fullName))
                continue;

            typeIdByFullName[fullName] = typeId;
            typeInfoBySymbol[tsym] = new TypeBuildInfo(tsym, typeId, fullName);
        }

        var namespaceIds = new Dictionary<string, string>(StringComparer.Ordinal);

        var allMethods = new List<MethodBuildInfo>();

        foreach (var info in typeInfoBySymbol.Values.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();

            var ns = ExtractNamespace(info.FullName);
            if (!namespaceIds.TryGetValue(ns, out var namespaceId))
            {
                namespaceId = GraphIdFactory.CreateNamespaceId(projectId, ns);
                namespaceIds[ns] = namespaceId;

                nodes[namespaceId] = new GraphNode(
                    namespaceId,
                    GraphNodeKind.Namespace,
                    ns,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                );

                edges.Add(new GraphEdge(
                    projectId,
                    namespaceId,
                    GraphEdgeKind.Contains,
                    Label: null,
                    Attributes: new Dictionary<string, string>(StringComparer.Ordinal),
                    Evidence: null,
                    Confidence: 1.0
                ));
            }

            var methods = BuildTypeNode(info, compilation, typeIdByFullName, nodes, edges, ct);
            allMethods.AddRange(methods);

            edges.Add(new GraphEdge(
                namespaceId,
                info.TypeId,
                GraphEdgeKind.Contains,
                Label: null,
                Attributes: new Dictionary<string, string>(StringComparer.Ordinal),
                Evidence: null,
                Confidence: 1.0
            ));
        }

        AddCallEdges(allMethods, nodes, edges);
    }

    private static IReadOnlyList<MethodBuildInfo> BuildTypeNode(
        TypeBuildInfo info,
        Compilation compilation,
        IReadOnlyDictionary<string, string> typeIdByFullName,
        IDictionary<string, GraphNode> nodes,
        IList<GraphEdge> edges,
        CancellationToken ct)
    {
        var tsym = info.Symbol;

        var fields = tsym.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsImplicitlyDeclared)
            .Select(f => new FieldBuildInfo(
                Name: f.Name,
                TypeName: NormalizeTypeName(f.Type),
                IsPublic: f.DeclaredAccessibility == Accessibility.Public))
            .ToList();

        var methods = BuildMethods(info.TypeId, tsym, compilation, ct);

        var deps = ComputeTypeDependencies(tsym, compilation, ct);

        var attrs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [GraphAttributeKeys.TypeKind] = MapTypeKind(tsym).ToString(),
            [GraphAttributeKeys.IsAbstract] = (tsym.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface || tsym.IsAbstract).ToString(),
            [GraphAttributeKeys.FieldNames] = JsonSerializer.Serialize(fields.Select(f => f.Name)),
            [GraphAttributeKeys.FieldTypes] = JsonSerializer.Serialize(fields.Select(f => f.TypeName)),
            [GraphAttributeKeys.FieldIsPublic] = JsonSerializer.Serialize(fields.Select(f => f.IsPublic)),
            [GraphAttributeKeys.DependsOnTypes] = JsonSerializer.Serialize(deps)
        };

        nodes[info.TypeId] = new GraphNode(
            info.TypeId,
            GraphNodeKind.Type,
            info.FullName,
            attrs
        );

        foreach (var method in methods)
        {
            nodes[method.MethodId] = new GraphNode(
                method.MethodId,
                GraphNodeKind.Method,
                method.FullName,
                method.Attributes
            );

            edges.Add(new GraphEdge(
                info.TypeId,
                method.MethodId,
                GraphEdgeKind.Contains,
                Label: null,
                Attributes: new Dictionary<string, string>(StringComparer.Ordinal),
                Evidence: null,
                Confidence: 1.0
            ));
        }

        AddInheritanceEdges(info, typeIdByFullName, edges);
        AddReferenceEdges(info, deps, typeIdByFullName, edges);
        return methods;
    }

    private static void AddInheritanceEdges(
        TypeBuildInfo info,
        IReadOnlyDictionary<string, string> typeIdByFullName,
        IList<GraphEdge> edges)
    {
        var tsym = info.Symbol;

        if (tsym.BaseType is INamedTypeSymbol bt && bt.SpecialType != SpecialType.System_Object)
        {
            var baseFullName = GetTypeFullName(bt);
            if (typeIdByFullName.TryGetValue(baseFullName, out var baseId))
            {
                edges.Add(new GraphEdge(
                    info.TypeId,
                    baseId,
                    GraphEdgeKind.Inherits,
                    Label: null,
                    Attributes: new Dictionary<string, string>(StringComparer.Ordinal),
                    Evidence: null,
                    Confidence: 1.0
                ));
            }
        }

        foreach (var iface in tsym.Interfaces)
        {
            var ifaceFullName = GetTypeFullName(iface);
            if (typeIdByFullName.TryGetValue(ifaceFullName, out var ifaceId))
            {
                edges.Add(new GraphEdge(
                    info.TypeId,
                    ifaceId,
                    GraphEdgeKind.Implements,
                    Label: null,
                    Attributes: new Dictionary<string, string>(StringComparer.Ordinal),
                    Evidence: null,
                    Confidence: 1.0
                ));
            }
        }
    }

    private static void AddReferenceEdges(
        TypeBuildInfo info,
        IReadOnlyList<string> deps,
        IReadOnlyDictionary<string, string> typeIdByFullName,
        IList<GraphEdge> edges)
    {
        foreach (var dep in deps.Distinct(StringComparer.Ordinal))
        {
            if (typeIdByFullName.TryGetValue(dep, out var depId))
            {
                edges.Add(new GraphEdge(
                    info.TypeId,
                    depId,
                    GraphEdgeKind.ReferencesType,
                    Label: null,
                    Attributes: new Dictionary<string, string>(StringComparer.Ordinal),
                    Evidence: null,
                    Confidence: 1.0
                ));
            }
        }
    }

    private static void AddCallEdges(
        IReadOnlyList<MethodBuildInfo> methods,
        IDictionary<string, GraphNode> nodes,
        IList<GraphEdge> edges)
    {
        var methodIdBySymbol = methods
            .Where(m => m.Symbol is not null)
            .ToDictionary(m => (IMethodSymbol)m.Symbol!, m => m.MethodId, SymbolEqualityComparer.Default);

        foreach (var method in methods)
        {
            if (method.InvocationTargets.Count == 0) continue;
            if (method.Symbol is null) continue;

            var externalCalls = new List<string>();
            foreach (var target in method.InvocationTargets)
            {
                if (methodIdBySymbol.TryGetValue(target, out var targetId))
                {
                    edges.Add(new GraphEdge(
                        method.MethodId,
                        targetId,
                        GraphEdgeKind.Calls,
                        Label: null,
                        Attributes: new Dictionary<string, string>(StringComparer.Ordinal),
                        Evidence: null,
                        Confidence: 1.0
                    ));
                }
                else
                {
                    externalCalls.Add(GetMethodFullName(target));
                }
            }

            if (externalCalls.Count > 0 && nodes.TryGetValue(method.MethodId, out var node))
            {
                var attrs = new Dictionary<string, string>(node.Attributes, StringComparer.Ordinal)
                {
                    [GraphAttributeKeys.MethodExternalCalls] = JsonSerializer.Serialize(externalCalls.Distinct(StringComparer.Ordinal))
                };
                nodes[method.MethodId] = node with { Attributes = attrs };
            }
        }
    }

    private static List<MethodBuildInfo> BuildMethods(
        string typeId,
        INamedTypeSymbol tsym,
        Compilation compilation,
        CancellationToken ct)
    {
        var list = new List<MethodBuildInfo>();

        foreach (var msym in tsym.GetMembers().OfType<IMethodSymbol>())
        {
            ct.ThrowIfCancellationRequested();

            if (msym.IsImplicitlyDeclared) continue;
            if (msym.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor)) continue;

            var methodFullName = GetMethodFullName(msym);
            var signature = GetMethodSignatureStable(msym);
            var methodId = GraphIdFactory.CreateMethodId(typeId, signature);

            var declRef = msym.DeclaringSyntaxReferences.FirstOrDefault();
            if (declRef is null)
            {
                list.Add(MethodBuildInfo.WithDefaults(msym, methodId, methodFullName));
                continue;
            }

            var syntax = declRef.GetSyntax(ct);
            var sloc = SlocCounter.Count(syntax.ToFullString());
            var nbd = NestingDepthWalker.Compute(syntax);
            var calls = InvocationWalker.Compute(syntax);

            var smForNode = compilation.GetSemanticModel(syntax.SyntaxTree);
            var accessed = FieldAccessWalker.Compute(syntax, smForNode, tsym);

            var invocationTargets = new List<IMethodSymbol>();
            foreach (var invocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var info = smForNode.GetSymbolInfo(invocation, ct).Symbol as IMethodSymbol;
                if (info is null) continue;
                invocationTargets.Add(info);
            }

            var attrs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GraphAttributeKeys.MethodParameters] = msym.Parameters.Length.ToString(),
                [GraphAttributeKeys.MethodSloc] = sloc.ToString(),
                [GraphAttributeKeys.MethodDecisionPoints] = "0",
                [GraphAttributeKeys.MethodMaxNestingDepth] = nbd.ToString(),
                [GraphAttributeKeys.MethodCalls] = calls.ToString(),
                [GraphAttributeKeys.MethodIsPublic] = (msym.DeclaredAccessibility == Accessibility.Public).ToString(),
                [GraphAttributeKeys.MethodAccessedFields] = JsonSerializer.Serialize(accessed),
                [GraphAttributeKeys.MethodExternalCalls] = "[]"
            };

            list.Add(new MethodBuildInfo(msym, methodId, methodFullName, attrs, invocationTargets));
        }

        return list;
    }

    private static List<INamedTypeSymbol> CollectDeclaredTypes(Compilation compilation, CancellationToken ct)
    {
        var acc = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();

            var sm = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot(ct);

            foreach (var decl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                var tsym = sm.GetDeclaredSymbol(decl, ct) as INamedTypeSymbol;
                if (tsym is null || tsym.IsImplicitlyDeclared) continue;
                acc.Add(tsym);
            }
        }

        return acc.ToList();
    }

    private static List<string> ComputeTypeDependencies(INamedTypeSymbol tsym, Compilation compilation, CancellationToken ct)
    {
        var deps = new HashSet<string>(StringComparer.Ordinal);

        if (tsym.BaseType is INamedTypeSymbol bt && bt.SpecialType != SpecialType.System_Object)
            deps.Add(GetTypeFullName(bt));
        foreach (var iface in tsym.Interfaces)
            deps.Add(GetTypeFullName(iface));

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

        foreach (var sr in tsym.DeclaringSyntaxReferences)
        {
            ct.ThrowIfCancellationRequested();

            if (sr.GetSyntax(ct) is not BaseTypeDeclarationSyntax decl) continue;

            foreach (var newExpr in decl.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var sm = compilation.GetSemanticModel(newExpr.SyntaxTree);
                var ctor = sm.GetSymbolInfo(newExpr, ct).Symbol as IMethodSymbol;
                var target = ctor?.ContainingType;
                if (target is INamedTypeSymbol nt)
                    deps.Add(GetTypeFullName(nt));
            }

            foreach (var typeSyntax in decl.DescendantNodes().OfType<TypeSyntax>())
            {
                var sm = compilation.GetSemanticModel(typeSyntax.SyntaxTree);
                var typeInfo = sm.GetTypeInfo(typeSyntax, ct).Type as INamedTypeSymbol;
                if (typeInfo is not null)
                    deps.Add(GetTypeFullName(typeInfo));
            }
        }

        deps.Remove(GetTypeFullName(tsym));
        deps.RemoveWhere(string.IsNullOrWhiteSpace);
        return deps.ToList();
    }

    private static string ResolveSolutionName(RoslynWorkspaceResult workspace)
    {
        if (!string.IsNullOrWhiteSpace(workspace.SolutionPath))
            return Path.GetFileNameWithoutExtension(workspace.SolutionPath);
        return "Workspace";
    }

    private static string ResolveProjectRelativePath(string rootPath, string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return "workspace";

        try
        {
            var relative = Path.GetRelativePath(rootPath, projectPath);
            return GraphIdFactory.NormalizePath(relative);
        }
        catch
        {
            return GraphIdFactory.NormalizePath(projectPath);
        }
    }

    private static string GetTypeFullName(INamedTypeSymbol t)
    {
        var s = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return GraphIdFactory.TrimGlobalPrefix(s);
    }

    private static string GetMethodFullName(IMethodSymbol m)
        => m.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    private static string NormalizeTypeName(ITypeSymbol? t)
    {
        if (t is null) return string.Empty;
        if (t is INamedTypeSymbol nt)
        {
            var s = nt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return GraphIdFactory.TrimGlobalPrefix(s);
        }
        return GraphIdFactory.TrimGlobalPrefix(t.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
    }

    private static string GetTypeMetadataName(INamedTypeSymbol t)
    {
        var s = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return GraphIdFactory.TrimGlobalPrefix(s);
    }

    private static string GetMethodSignatureStable(IMethodSymbol methodSymbol)
    {
        var name = methodSymbol.MethodKind == MethodKind.Constructor ? ".ctor" : methodSymbol.Name;
        var arity = methodSymbol.IsGenericMethod ? $"`{methodSymbol.TypeParameters.Length}" : string.Empty;
        var paramTypes = methodSymbol.Parameters.Select(p => NormalizeTypeName(p.Type)).ToArray();
        var returnType = NormalizeTypeName(methodSymbol.ReturnType);
        var signature = $"{name}{arity}({string.Join(",", paramTypes)})";
        if (!string.IsNullOrWhiteSpace(returnType))
            signature = $"{signature}:{returnType}";
        return signature;
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

    private sealed record FieldBuildInfo(string Name, string TypeName, bool IsPublic);

    private sealed record MethodBuildInfo(
        IMethodSymbol? Symbol,
        string MethodId,
        string FullName,
        IReadOnlyDictionary<string, string> Attributes,
        IReadOnlyList<IMethodSymbol> InvocationTargets)
    {
        public static MethodBuildInfo WithDefaults(IMethodSymbol symbol, string methodId, string fullName)
        {
            var attrs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GraphAttributeKeys.MethodParameters] = symbol.Parameters.Length.ToString(),
                [GraphAttributeKeys.MethodSloc] = "0",
                [GraphAttributeKeys.MethodDecisionPoints] = "0",
                [GraphAttributeKeys.MethodMaxNestingDepth] = "0",
                [GraphAttributeKeys.MethodCalls] = "0",
                [GraphAttributeKeys.MethodIsPublic] = (symbol.DeclaredAccessibility == Accessibility.Public).ToString(),
                [GraphAttributeKeys.MethodAccessedFields] = "[]",
                [GraphAttributeKeys.MethodExternalCalls] = "[]"
            };
            return new MethodBuildInfo(symbol, methodId, fullName, attrs, Array.Empty<IMethodSymbol>());
        }
    }

    private sealed record TypeBuildInfo(INamedTypeSymbol Symbol, string TypeId, string FullName);
}

