using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

/// <summary>
/// Computes solution-level dependency metrics:
/// - DEP: total distinct dependency edges (type -> target), including external targets
/// - I-DEP: total distinct internal dependency edges (type -> type within the model)
/// - Cycles: strongly connected components (>1) for Types and Namespaces
/// </summary>
public sealed class DependenciesMetricsCalculator
{
    #region Public API

    public DependencyMetrics Compute(CodeModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        var allTypes = CollectTypes(model).Select(t => t.FullName).ToHashSet(StringComparer.Ordinal);

        // DEP: distinct (source -> target) from TypeNode.DependsOnTypes (includes externals)
        var depEdges = BuildDistinctEdgesFromNodes(model, allTypes);
        var totalDep = depEdges.Count;

        // I-DEP: distinct internal edges from the internal graph (TypeEdges)
        var internalEdges = BuildDistinctInternalEdges(model.DependencyGraph.TypeEdges);
        var totalIDep = internalEdges.Count;

        // Cycles
        var typeCycles = FindCycles(model.DependencyGraph.TypeEdges, allTypes)
            .Select(nodes => new DependencyCycle(nodes, "Type"));
        var nsNames = CollectNamespaces(model).Select(n => n.Name).ToHashSet(StringComparer.Ordinal);
        var nsCycles = FindCycles(model.DependencyGraph.NamespaceEdges, nsNames)
            .Select(nodes => new DependencyCycle(nodes, "Namespace"));

        var cycles = typeCycles.Concat(nsCycles).ToList();

        return new DependencyMetrics(
            TotalDependencies: totalDep,
            InternalDependencies: totalIDep,
            Cycles: cycles
        );
    }

    #endregion

    #region Helpers (collections)

    private static IEnumerable<TypeNode> CollectTypes(CodeModel model) =>
        model.Codebase.Modules.SelectMany(m => m.Namespaces).SelectMany(n => n.Types);

    private static IEnumerable<NamespaceNode> CollectNamespaces(CodeModel model) =>
        model.Codebase.Modules.SelectMany(m => m.Namespaces);

    private static HashSet<(string source, string target)> BuildDistinctEdgesFromNodes(CodeModel model, HashSet<string> allTypes)
    {
        var set = new HashSet<(string, string)>();
        foreach (var type in CollectTypes(model))
        {
            if (type.DependsOnTypes is null) continue;
            foreach (var target in type.DependsOnTypes)
            {
                if (string.IsNullOrWhiteSpace(target)) continue;
                set.Add((type.FullName, target));
            }
        }
        return set;
    }

    private static HashSet<(string source, string target)> BuildDistinctInternalEdges(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> graph)
    {
        var set = new HashSet<(string, string)>();
        foreach (var (src, targets) in graph)
        {
            if (targets is null) continue;
            foreach (var t in targets)
            {
                if (string.Equals(src, t, StringComparison.Ordinal)) continue; // skip self-loop as a single edge
                set.Add((src, t));
            }
        }
        return set;
    }

    #endregion

    #region Helpers (cycles / SCC)

    private static IReadOnlyList<IReadOnlyList<string>> FindCycles(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> graph,
        HashSet<string> universe)
    {
        // Normalize to ensure all nodes exist in dictionary
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var n in universe) map[n] = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kv in graph)
        {
            if (!map.TryGetValue(kv.Key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                map[kv.Key] = set;
            }
            foreach (var t in kv.Value ?? Array.Empty<string>())
            {
                if (universe.Contains(t))
                    set.Add(t);
            }
        }

        // Tarjan SCC
        return Tarjan(map)
            .Where(scc => scc.Count > 1) // cycles: only SCCs with >1 nodes
            .Select(scc => (IReadOnlyList<string>)scc)
            .ToList();
    }

    private static List<List<string>> Tarjan(Dictionary<string, HashSet<string>> graph)
    {
        var index = 0;
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var idx = new Dictionary<string, int>(StringComparer.Ordinal);
        var low = new Dictionary<string, int>(StringComparer.Ordinal);
        var sccs = new List<List<string>>();

        void StrongConnect(string v)
        {
            idx[v] = index;
            low[v] = index;
            index++;
            stack.Push(v);
            onStack.Add(v);

            foreach (var w in graph[v])
            {
                if (!idx.ContainsKey(w))
                {
                    StrongConnect(w);
                    low[v] = Math.Min(low[v], low[w]);
                }
                else if (onStack.Contains(w))
                {
                    low[v] = Math.Min(low[v], idx[w]);
                }
            }

            if (low[v] == idx[v])
            {
                var comp = new List<string>();
                string w;
                do
                {
                    w = stack.Pop();
                    onStack.Remove(w);
                    comp.Add(w);
                } while (!string.Equals(w, v, StringComparison.Ordinal));

                sccs.Add(comp);
            }
        }

        foreach (var v in graph.Keys)
            if (!idx.ContainsKey(v))
                StrongConnect(v);

        return sccs;
    }

    #endregion
}
