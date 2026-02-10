using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sharpscope.Domain.Models;

namespace Sharpscope.Test.Helpers;

public static class TestGraphFactory
{
    public static CodeGraph FromCodeModel(CodeModel model)
    {
        var nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var edges = new List<GraphEdge>();

        var solutionId = GraphIdFactory.CreateSolutionId("TestSolution");
        var projectId = GraphIdFactory.CreateProjectId("test.csproj");

        nodes[solutionId] = new GraphNode(solutionId, GraphNodeKind.Solution, "TestSolution", new Dictionary<string, string>());
        nodes[projectId] = new GraphNode(projectId, GraphNodeKind.Project, "TestProject", new Dictionary<string, string>());
        edges.Add(NewEdge(solutionId, projectId, GraphEdgeKind.Contains));

        foreach (var ns in model.Codebase.Modules.SelectMany(m => m.Namespaces))
        {
            var nsId = GraphIdFactory.CreateNamespaceId(projectId, ns.Name);
            nodes[nsId] = new GraphNode(nsId, GraphNodeKind.Namespace, ns.Name, new Dictionary<string, string>());
            edges.Add(NewEdge(projectId, nsId, GraphEdgeKind.Contains));

            foreach (var type in ns.Types)
            {
                var typeId = GraphIdFactory.CreateTypeId(projectId, type.FullName);
                nodes[typeId] = new GraphNode(typeId, GraphNodeKind.Type, type.FullName, new Dictionary<string, string>
                {
                    [GraphAttributeKeys.TypeKind] = type.Kind.ToString(),
                    [GraphAttributeKeys.IsAbstract] = type.IsAbstract.ToString(),
                    [GraphAttributeKeys.FieldNames] = JsonSerializer.Serialize(type.Fields.Select(f => f.Name)),
                    [GraphAttributeKeys.FieldTypes] = JsonSerializer.Serialize(type.Fields.Select(f => f.TypeName)),
                    [GraphAttributeKeys.FieldIsPublic] = JsonSerializer.Serialize(type.Fields.Select(f => f.IsPublic)),
                    [GraphAttributeKeys.DependsOnTypes] = JsonSerializer.Serialize(type.DependsOnTypes ?? Array.Empty<string>())
                });
                edges.Add(NewEdge(nsId, typeId, GraphEdgeKind.Contains));

                foreach (var method in type.Methods)
                {
                    var methodId = GraphIdFactory.CreateMethodId(typeId, method.FullName);
                    nodes[methodId] = new GraphNode(methodId, GraphNodeKind.Method, method.FullName, new Dictionary<string, string>
                    {
                        [GraphAttributeKeys.MethodParameters] = method.Parameters.ToString(),
                        [GraphAttributeKeys.MethodSloc] = method.Sloc.ToString(),
                        [GraphAttributeKeys.MethodDecisionPoints] = method.DecisionPoints.ToString(),
                        [GraphAttributeKeys.MethodMaxNestingDepth] = method.MaxNestingDepth.ToString(),
                        [GraphAttributeKeys.MethodCalls] = method.Calls.ToString(),
                        [GraphAttributeKeys.MethodIsPublic] = method.IsPublic.ToString(),
                        [GraphAttributeKeys.MethodAccessedFields] = JsonSerializer.Serialize(method.AccessedFields ?? Array.Empty<string>()),
                        [GraphAttributeKeys.MethodExternalCalls] = "[]"
                    });
                    edges.Add(NewEdge(typeId, methodId, GraphEdgeKind.Contains));
                }
            }
        }

        // Reference edges from dependency graph (internal only)
        var typeIdByName = nodes.Values
            .Where(n => n.Kind == GraphNodeKind.Type)
            .ToDictionary(n => n.Name, n => n.Id, StringComparer.Ordinal);

        foreach (var (src, targets) in model.DependencyGraph.TypeEdges)
        {
            if (!typeIdByName.TryGetValue(src, out var srcId)) continue;
            foreach (var target in targets)
            {
                if (!typeIdByName.TryGetValue(target, out var tgtId)) continue;
                edges.Add(NewEdge(srcId, tgtId, GraphEdgeKind.ReferencesType));
            }
        }

        return new CodeGraph(nodes, edges);
    }

    private static GraphEdge NewEdge(string from, string to, GraphEdgeKind kind) =>
        new(from, to, kind, Label: null, new Dictionary<string, string>(), Evidence: null, Confidence: 1.0);
}

