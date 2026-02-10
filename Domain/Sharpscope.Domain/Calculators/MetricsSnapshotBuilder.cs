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
        var projectNodes = graph.Nodes.Values.Where(n => n.Kind == GraphNodeKind.Project).ToList();

        var methodMetrics = MapByNodeName(
            graph,
            GraphNodeKind.Method,
            methods,
            metric => metric.MethodFullName);

        var typeMetrics = MapByNodeName(
            graph,
            GraphNodeKind.Type,
            types,
            metric => metric.TypeFullName);

        var namespaceMetrics = MapByNodeName(
            graph,
            GraphNodeKind.Namespace,
            namespaces,
            metric => metric.Namespace);

        var namespaceCouplingMetrics = MapByNodeName(
            graph,
            GraphNodeKind.Namespace,
            namespaceCoupling,
            metric => metric.Namespace);

        var typeCouplingMetrics = MapByNodeName(
            graph,
            GraphNodeKind.Type,
            typeCoupling,
            metric => metric.TypeFullName);

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

    private static IReadOnlyDictionary<string, TMetric> MapByNodeName<TMetric>(
        CodeGraph graph,
        GraphNodeKind kind,
        IReadOnlyList<TMetric> metrics,
        Func<TMetric, string> metricName)
    {
        var idsByName = graph.Nodes.Values
            .Where(n => n.Kind == kind)
            .GroupBy(n => n.Name, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(n => n.Id).OrderBy(id => id, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var metricsByName = metrics
            .GroupBy(metricName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var result = new Dictionary<string, TMetric>(StringComparer.Ordinal);

        foreach (var (name, ids) in idsByName)
        {
            if (!metricsByName.TryGetValue(name, out var list))
                continue;

            var count = Math.Min(ids.Count, list.Count);
            for (var i = 0; i < count; i++)
                result[ids[i]] = list[i];
        }

        return result;
    }
}
