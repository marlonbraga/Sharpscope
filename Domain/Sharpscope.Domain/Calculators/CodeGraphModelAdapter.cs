using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

internal static class CodeGraphModelAdapter
{
    // Pre-existing legacy debt: cognitive complexity 26 vs. the 15 allowed by the Code Quality
    // principle (constitution). Suppressed here rather than lowering the gate for everyone;
    // refactor this method (with a characterization test first, per Principle I) the next time
    // it needs to change.
#pragma warning disable S3776
    public static CodeModel ToCodeModel(CodeGraph graph)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));

        var nodes = graph.Nodes;
        var containsMap = graph.Edges
            .Where(e => e.Kind == GraphEdgeKind.Contains)
            .GroupBy(e => e.FromId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ToId).Distinct(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var modules = new List<ModuleNode>();

        foreach (var project in nodes.Values.Where(n => n.Kind == GraphNodeKind.Project)
                     .OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            var namespaces = new List<NamespaceNode>();
            foreach (var nsId in GetChildren(project.Id, containsMap))
            {
                if (!nodes.TryGetValue(nsId, out var nsNode) || nsNode.Kind != GraphNodeKind.Namespace)
                    continue;

                var types = new List<TypeNode>();
                foreach (var typeId in GetChildren(nsId, containsMap))
                {
                    if (!nodes.TryGetValue(typeId, out var typeNode) || typeNode.Kind != GraphNodeKind.Type)
                        continue;

                    var methods = new List<MethodNode>();
                    foreach (var methodId in GetChildren(typeId, containsMap))
                    {
                        if (!nodes.TryGetValue(methodId, out var methodNode) || methodNode.Kind != GraphNodeKind.Method)
                            continue;

                        methods.Add(BuildMethodNode(methodNode));
                    }

                    types.Add(BuildTypeNode(typeNode, methods));
                }

                namespaces.Add(new NamespaceNode(nsNode.Name, types));
            }

            modules.Add(new ModuleNode(project.Name, namespaces));
        }

        var codebase = new Codebase(modules);
        var dependencyGraph = BuildDependencyGraph(nodes, graph.Edges);
        return new CodeModel(codebase, dependencyGraph);
    }
#pragma warning restore S3776

    private static DependencyGraph BuildDependencyGraph(
        IReadOnlyDictionary<string, GraphNode> nodes,
        IReadOnlyList<GraphEdge> edges)
    {
        var typeIdToName = nodes.Values
            .Where(n => n.Kind == GraphNodeKind.Type)
            .ToDictionary(n => n.Id, n => n.Name, StringComparer.Ordinal);

        var typeEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var edge in edges.Where(e => e.Kind == GraphEdgeKind.ReferencesType))
        {
            if (!typeIdToName.TryGetValue(edge.FromId, out var srcName)) continue;
            if (!typeIdToName.TryGetValue(edge.ToId, out var tgtName)) continue;

            if (!typeEdges.TryGetValue(srcName, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                typeEdges[srcName] = set;
            }
            set.Add(tgtName);
        }

        foreach (var typeName in typeIdToName.Values)
        {
            if (!typeEdges.ContainsKey(typeName))
                typeEdges[typeName] = new HashSet<string>(StringComparer.Ordinal);
        }

        var nsByType = nodes.Values
            .Where(n => n.Kind == GraphNodeKind.Type)
            .GroupBy(n => n.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => ExtractNamespace(g.Key), StringComparer.Ordinal);

        var namespaceEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var ns in nsByType.Values.Distinct(StringComparer.Ordinal))
            namespaceEdges[ns] = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (srcType, targets) in typeEdges)
        {
            var srcNs = nsByType[srcType];
            foreach (var tgt in targets)
            {
                var tgtNs = nsByType[tgt];
                if (!string.Equals(srcNs, tgtNs, StringComparison.Ordinal))
                    namespaceEdges[srcNs].Add(tgtNs);
            }
        }

        var typeEdgesReadonly = typeEdges.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyCollection<string>)kv.Value,
            StringComparer.Ordinal);

        var nsEdgesReadonly = namespaceEdges.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyCollection<string>)kv.Value,
            StringComparer.Ordinal);

        return new DependencyGraph(typeEdgesReadonly, nsEdgesReadonly);
    }

    private static TypeNode BuildTypeNode(GraphNode typeNode, IReadOnlyList<MethodNode> methods)
    {
        var kind = Enum.TryParse<TypeKind>(GetAttr(typeNode, GraphAttributeKeys.TypeKind), out var k)
            ? k
            : TypeKind.Class;

        var isAbstract = bool.TryParse(GetAttr(typeNode, GraphAttributeKeys.IsAbstract), out var abs) && abs;

        var fieldNames = DeserializeStringList(GetAttr(typeNode, GraphAttributeKeys.FieldNames));
        var fieldTypes = DeserializeStringList(GetAttr(typeNode, GraphAttributeKeys.FieldTypes));
        var fieldIsPublic = DeserializeBoolList(GetAttr(typeNode, GraphAttributeKeys.FieldIsPublic));

        var fields = new List<FieldNode>();
        for (var i = 0; i < fieldNames.Count; i++)
        {
            var name = fieldNames[i];
            var typeName = i < fieldTypes.Count ? fieldTypes[i] : string.Empty;
            var isPublic = i < fieldIsPublic.Count && fieldIsPublic[i];
            fields.Add(new FieldNode(name, typeName, isPublic));
        }

        var dependsOnTypes = DeserializeStringList(GetAttr(typeNode, GraphAttributeKeys.DependsOnTypes));

        return new TypeNode(
            FullName: typeNode.Name,
            Kind: kind,
            IsAbstract: isAbstract,
            Fields: fields,
            Methods: methods,
            DependsOnTypes: dependsOnTypes
        );
    }

    private static MethodNode BuildMethodNode(GraphNode methodNode)
    {
        var parameters = ParseInt(GetAttr(methodNode, GraphAttributeKeys.MethodParameters));
        var sloc = ParseInt(GetAttr(methodNode, GraphAttributeKeys.MethodSloc));
        var decisionPoints = ParseInt(GetAttr(methodNode, GraphAttributeKeys.MethodDecisionPoints));
        var maxNesting = ParseInt(GetAttr(methodNode, GraphAttributeKeys.MethodMaxNestingDepth));
        var calls = ParseInt(GetAttr(methodNode, GraphAttributeKeys.MethodCalls));
        var isPublic = bool.TryParse(GetAttr(methodNode, GraphAttributeKeys.MethodIsPublic), out var pub) && pub;
        var accessed = DeserializeStringList(GetAttr(methodNode, GraphAttributeKeys.MethodAccessedFields));

        return new MethodNode(
            FullName: methodNode.Name,
            Parameters: parameters,
            Sloc: sloc,
            DecisionPoints: decisionPoints,
            MaxNestingDepth: maxNesting,
            Calls: calls,
            IsPublic: isPublic,
            AccessedFields: accessed
        );
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

    private static List<bool> DeserializeBoolList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<bool>();
        try
        {
            return JsonSerializer.Deserialize<List<bool>>(json) ?? new List<bool>();
        }
        catch
        {
            return new List<bool>();
        }
    }

    private static string? GetAttr(GraphNode node, string key)
        => node.Attributes.TryGetValue(key, out var v) ? v : null;

    private static int ParseInt(string? value)
        => int.TryParse(value, out var v) ? v : 0;

    private static IReadOnlyList<string> GetChildren(
        string nodeId,
        IReadOnlyDictionary<string, List<string>> containsMap)
        => containsMap.TryGetValue(nodeId, out var children) ? children : Array.Empty<string>();

    private static string ExtractNamespace(string fullName)
    {
        var idx = fullName.LastIndexOf('.');
        return idx <= 0 ? string.Empty : fullName.Substring(0, idx);
    }
}
