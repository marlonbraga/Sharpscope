using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

internal static class MetricsSnapshotBuilder
{
    public static MetricsSnapshot Build(
        CodeGraph graph,
        SummaryMetrics summary,
        IReadOnlyList<NamespaceMetrics> namespaces,
        IReadOnlyList<TypeMetrics> types,
        IReadOnlyList<MethodMetrics> methods,
        IReadOnlyList<NamespaceCouplingMetrics> namespaceCoupling,
        IReadOnlyList<TypeCouplingMetrics> typeCoupling,
        DependencyMetrics dependencies)
    {
        var methodIdByName = graph.Nodes.Values
            .Where(n => n.Kind == GraphNodeKind.Method)
            .GroupBy(n => n.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var typeIdByName = graph.Nodes.Values
            .Where(n => n.Kind == GraphNodeKind.Type)
            .GroupBy(n => n.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var namespaceIdByName = graph.Nodes.Values
            .Where(n => n.Kind == GraphNodeKind.Namespace)
            .GroupBy(n => n.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var projectNodes = graph.Nodes.Values.Where(n => n.Kind == GraphNodeKind.Project).ToList();

        var methodMetrics = new Dictionary<string, MethodMetrics>(StringComparer.Ordinal);
        foreach (var metric in methods)
        {
            if (methodIdByName.TryGetValue(metric.MethodFullName, out var id))
                methodMetrics[id] = metric;
        }

        var typeMetrics = new Dictionary<string, TypeMetrics>(StringComparer.Ordinal);
        foreach (var metric in types)
        {
            if (typeIdByName.TryGetValue(metric.TypeFullName, out var id))
                typeMetrics[id] = metric;
        }

        var namespaceMetrics = new Dictionary<string, NamespaceMetrics>(StringComparer.Ordinal);
        foreach (var metric in namespaces)
        {
            if (namespaceIdByName.TryGetValue(metric.Namespace, out var id))
                namespaceMetrics[id] = metric;
        }

        var namespaceCouplingMetrics = new Dictionary<string, NamespaceCouplingMetrics>(StringComparer.Ordinal);
        foreach (var metric in namespaceCoupling)
        {
            if (namespaceIdByName.TryGetValue(metric.Namespace, out var id))
                namespaceCouplingMetrics[id] = metric;
        }

        var typeCouplingMetrics = new Dictionary<string, TypeCouplingMetrics>(StringComparer.Ordinal);
        foreach (var metric in typeCoupling)
        {
            if (typeIdByName.TryGetValue(metric.TypeFullName, out var id))
                typeCouplingMetrics[id] = metric;
        }

        var projectMetrics = BuildProjectMetrics(graph, projectNodes);

        return new MetricsSnapshot(
            Methods: methodMetrics,
            Types: typeMetrics,
            Namespaces: namespaceMetrics,
            Projects: projectMetrics,
            Summary: summary,
            NamespaceCoupling: namespaceCouplingMetrics,
            TypeCoupling: typeCouplingMetrics,
            Dependencies: dependencies
        );
    }

    private static IReadOnlyDictionary<string, ProjectMetrics> BuildProjectMetrics(
        CodeGraph graph,
        IReadOnlyList<GraphNode> projectNodes)
    {
        var contains = graph.Edges
            .Where(e => e.Kind == GraphEdgeKind.Contains)
            .GroupBy(e => e.FromId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ToId).ToList(), StringComparer.Ordinal);

        var result = new Dictionary<string, ProjectMetrics>(StringComparer.Ordinal);
        foreach (var project in projectNodes)
        {
            var nsIds = GetChildren(project.Id, contains, GraphNodeKind.Namespace, graph);
            var typeIds = nsIds.SelectMany(id => GetChildren(id, contains, GraphNodeKind.Type, graph)).ToList();
            var methodIds = typeIds.SelectMany(id => GetChildren(id, contains, GraphNodeKind.Method, graph)).ToList();

            result[project.Id] = new ProjectMetrics(
                ProjectId: project.Id,
                Namespaces: nsIds.Count,
                Types: typeIds.Count,
                Methods: methodIds.Count
            );
        }

        return result;
    }

    private static IReadOnlyList<string> GetChildren(
        string nodeId,
        IReadOnlyDictionary<string, List<string>> contains,
        GraphNodeKind expectedKind,
        CodeGraph graph)
    {
        if (!contains.TryGetValue(nodeId, out var ids)) return Array.Empty<string>();
        return ids.Where(id => graph.Nodes.TryGetValue(id, out var n) && n.Kind == expectedKind).ToList();
    }
}
