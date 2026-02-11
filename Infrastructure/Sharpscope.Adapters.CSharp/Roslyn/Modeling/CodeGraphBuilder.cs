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
using Sharpscope.Infrastructure.Integrations;

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

        var dedupedEdges = edges
            .GroupBy(e => (e.FromId, e.ToId, e.Kind))
            .Select(g => g.First())
            .ToList();

        var orderedEdges = dedupedEdges
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

            if (nodes.TryGetValue(method.MethodId, out var node))
            {
                var existingCalls = DeserializeStringList(node.Attributes.TryGetValue(GraphAttributeKeys.MethodExternalCalls, out var json)
                    ? json
                    : "[]");
                externalCalls.AddRange(existingCalls);

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

            if (!IsIncludedMethod(msym)) continue;

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
            var invocationArguments = new List<InvocationArgumentInfo>();
            var fallbackExternalCalls = new List<string>();
            foreach (var invocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var info = smForNode.GetSymbolInfo(invocation, ct).Symbol as IMethodSymbol;
                if (info is not null)
                {
                    invocationTargets.Add(info);
                    if (ShouldCaptureInvocationArguments(info))
                        AddInvocationArguments(invocation.ArgumentList.Arguments, info, smForNode, invocationArguments, ct);
                }
                else
                {
                    var target = invocation.Expression.ToString();
                    if (ShouldCaptureInvocationArguments(target))
                        AddInvocationArguments(invocation.ArgumentList.Arguments, target, smForNode, invocationArguments, ct);
                    if (!string.IsNullOrWhiteSpace(target))
                        fallbackExternalCalls.Add(target);
                }
            }

            foreach (var creation in syntax.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var info = smForNode.GetSymbolInfo(creation, ct).Symbol as IMethodSymbol;
                if (info is null) continue;
                if (ShouldCaptureInvocationArguments(info))
                    AddInvocationArguments(creation.ArgumentList?.Arguments, info, smForNode, invocationArguments, ct);
            }

            foreach (var elementAccess in syntax.DescendantNodes().OfType<ElementAccessExpressionSyntax>())
            {
                var symbol = smForNode.GetSymbolInfo(elementAccess, ct).Symbol;
                if (symbol is null) continue;
                var target = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                if (!ShouldCaptureInvocationArguments(target)) continue;
                AddInvocationArguments(elementAccess.ArgumentList.Arguments, target, smForNode, invocationArguments, ct);
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
                [GraphAttributeKeys.MethodExternalCalls] = JsonSerializer.Serialize(fallbackExternalCalls.Distinct(StringComparer.Ordinal)),
                [GraphAttributeKeys.MethodInvocationArguments] = JsonSerializer.Serialize(invocationArguments)
            };

            list.Add(new MethodBuildInfo(msym, methodId, methodFullName, attrs, invocationTargets, invocationArguments));
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
            if (string.Equals(relative, ".", StringComparison.Ordinal))
                return "workspace";
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
        var name = ResolveMethodStableName(methodSymbol);
        var arity = methodSymbol.IsGenericMethod ? $"`{methodSymbol.TypeParameters.Length}" : string.Empty;
        var paramTypes = methodSymbol.Parameters.Select(p => NormalizeTypeName(p.Type)).ToArray();
        var returnType = NormalizeTypeName(methodSymbol.ReturnType);
        var signature = $"{name}{arity}({string.Join(",", paramTypes)})";
        if (!string.IsNullOrWhiteSpace(returnType))
            signature = $"{signature}:{returnType}";
        return signature;
    }

    private static bool IsIncludedMethod(IMethodSymbol msym)
    {
        if (msym.IsImplicitlyDeclared) return false;

        return msym.MethodKind switch
        {
            MethodKind.Ordinary => true,
            MethodKind.Constructor => true,
            MethodKind.StaticConstructor => true,
            MethodKind.Destructor => true,
            MethodKind.PropertyGet => true,
            MethodKind.PropertySet => true,
            MethodKind.EventAdd => true,
            MethodKind.EventRemove => true,
            MethodKind.UserDefinedOperator => true,
            MethodKind.Conversion => true,
            MethodKind.ExplicitInterfaceImplementation => true,
            _ => false
        };
    }

    private static string ResolveMethodStableName(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.MethodKind == MethodKind.Constructor) return ".ctor";
        if (methodSymbol.MethodKind == MethodKind.StaticConstructor) return ".cctor";
        if (methodSymbol.MethodKind == MethodKind.Destructor) return ".dtor";

        var explicitImpl = methodSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
        if (explicitImpl is not null)
        {
            var iface = NormalizeTypeName(explicitImpl.ContainingType);
            return $"{iface}.{explicitImpl.Name}";
        }

        return methodSymbol.Name;
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
        IReadOnlyList<IMethodSymbol> InvocationTargets,
        IReadOnlyList<InvocationArgumentInfo> InvocationArguments)
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
                [GraphAttributeKeys.MethodExternalCalls] = "[]",
                [GraphAttributeKeys.MethodInvocationArguments] = "[]"
            };
            return new MethodBuildInfo(symbol, methodId, fullName, attrs, Array.Empty<IMethodSymbol>(), Array.Empty<InvocationArgumentInfo>());
        }
    }

    private sealed record TypeBuildInfo(INamedTypeSymbol Symbol, string TypeId, string FullName);

    private sealed record InvocationArgumentInfo(
        string Target,
        int ArgumentIndex,
        string? Value,
        bool IsResolved);

    private static bool ShouldCaptureInvocationArguments(IMethodSymbol symbol)
        => ShouldCaptureInvocationArguments(GetMethodFullName(symbol));

    private static bool ShouldCaptureInvocationArguments(string methodFullName)
    {
        if (string.IsNullOrWhiteSpace(methodFullName)) return false;

        return methodFullName.Contains("CreateSender", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("CreateProcessor", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("CreateReceiver", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("BasicPublish", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("BasicConsume", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("ServiceBusClient", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("EventGridPublisherClient", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("CosmosClient", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("OracleConnection", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("SqlConnection", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("NpgsqlConnection", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("MongoClient", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("BlobServiceClient", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("GetBlobContainerClient", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("ConnectionFactory", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("AddHttpClient", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("GrpcChannel.ForAddress", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("HttpHeaders.Add", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("HttpRequestHeaders.Add", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("GetEnvironmentVariable", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("GetConnectionString", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("IConfiguration", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("ISecretProvider", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("SecretClient", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("GetSecret", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("SetSecret", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("System.Uri", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddInvocationArguments(
        SeparatedSyntaxList<ArgumentSyntax>? arguments,
        IMethodSymbol targetSymbol,
        SemanticModel semanticModel,
        ICollection<InvocationArgumentInfo> sink,
        CancellationToken ct)
    {
        if (arguments is null || arguments.Value.Count == 0) return;
        var target = GetMethodFullName(targetSymbol);
        var offset = targetSymbol.IsExtensionMethod && targetSymbol.Parameters.Length == arguments.Value.Count + 1 ? 1 : 0;
        AddInvocationArguments(arguments.Value, target, semanticModel, sink, ct, targetSymbol.Parameters, offset);
    }

    private static void AddInvocationArguments(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        string target,
        SemanticModel semanticModel,
        ICollection<InvocationArgumentInfo> sink,
        CancellationToken ct,
        IReadOnlyList<IParameterSymbol>? parameters = null,
        int parameterOffset = 0)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            var arg = arguments[i];
            var expr = arg.Expression;
            if (expr is null) continue;

            var paramIndex = i + parameterOffset;
            if (parameters is not null && paramIndex < parameters.Count)
            {
                var paramType = parameters[paramIndex].Type;
                var isString = paramType.SpecialType == SpecialType.System_String;
                var isUri = paramType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Contains("System.Uri", StringComparison.OrdinalIgnoreCase);
                if (!isString && !isUri)
                    continue;
            }

            var resolved = TryResolveStringLiteral(expr, semanticModel, ct, out var value);
            var sanitized = resolved ? IntegrationSecretRedactor.Redact(value) : null;
            sink.Add(new InvocationArgumentInfo(target, i, sanitized, resolved));
        }
    }

    private static bool TryResolveStringLiteral(
        ExpressionSyntax expr,
        SemanticModel semanticModel,
        CancellationToken ct,
        out string? value)
    {
        value = null;
        var constant = semanticModel.GetConstantValue(expr, ct);
        if (constant.HasValue && constant.Value is string s)
        {
            value = s;
            return true;
        }

        if (expr is LiteralExpressionSyntax literal && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
        {
            value = literal.Token.ValueText;
            return true;
        }

        if (expr is ObjectCreationExpressionSyntax objCreation)
        {
            var typeInfo = semanticModel.GetTypeInfo(objCreation.Type, ct).Type;
            if (typeInfo is not null &&
                typeInfo.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Contains("System.Uri", StringComparison.OrdinalIgnoreCase))
            {
                var args = objCreation.ArgumentList?.Arguments;
                if (args is not null && args.Value.Count > 0)
                {
                    var first = args.Value[0].Expression;
                    if (TryResolveStringLiteral(first, semanticModel, ct, out var nested))
                    {
                        value = nested;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}

