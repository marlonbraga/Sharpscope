using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

/// <summary>
/// Computes coupling-related metrics for namespaces and types:
/// - Namespace: CA (afferent), CE (efferent), I (instability), A (abstractness), D (normalized distance)
/// - Type: DEP (all deps), I-DEP (internal deps), FAN-IN, FAN-OUT
/// </summary>
public sealed class CouplingMetricsCalculator
{
    /// <summary>
    /// Computes coupling metrics for each namespace in the model.
    /// </summary>
    public IReadOnlyList<NamespaceCouplingMetrics> ComputeNamespaceCoupling(CodeModel model)
    {
        var namespaces = CollectNamespaces(model);
        var nsNames = new HashSet<string>(namespaces.Select(n => n.Name), StringComparer.Ordinal);

        var nsOut = BuildNamespaceOutEdges(model, nsNames);
        var nsIn = BuildNamespaceInEdges(nsOut, nsNames);

        return ComputeNamespaceMetrics(namespaces, nsIn, nsOut);
    }

    /// <summary>
    /// Computes coupling metrics for each type (DEP, I-DEP, FAN-IN, FAN-OUT).
    /// </summary>
    public IReadOnlyList<TypeCouplingMetrics> ComputeTypeCoupling(CodeModel model)
    {
        var (allTypes, typeNames) = CollectTypes(model);

        var typeOutInternal = BuildTypeOutInternal(model, typeNames);
        var typeInInternal = BuildTypeInInternal(typeOutInternal, typeNames);

        return ComputeTypeMetrics(allTypes, typeOutInternal, typeInInternal);
    }

    #region Namespaces

    private static List<NamespaceNode> CollectNamespaces(CodeModel model) =>
        model.Codebase.Modules.SelectMany(m => m.Namespaces).ToList();

    private static Dictionary<string, HashSet<string>> BuildNamespaceOutEdges(
        CodeModel model,
        HashSet<string> allNsNames)
    {
        var nsOut = allNsNames.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var kv in model.DependencyGraph.NamespaceEdges)
        {
            if (!nsOut.TryGetValue(kv.Key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                nsOut[kv.Key] = set;
            }

            foreach (var target in kv.Value ?? Array.Empty<string>())
            {
                // Ignore self-loops
                if (!string.Equals(kv.Key, target, StringComparison.Ordinal))
                    set.Add(target);
            }
        }

        return nsOut;
    }

    private static Dictionary<string, HashSet<string>> BuildNamespaceInEdges(
        Dictionary<string, HashSet<string>> nsOut,
        HashSet<string> allNsNames)
    {
        var nsIn = allNsNames.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var (source, targets) in nsOut)
        {
            foreach (var t in targets)
            {
                if (!nsIn.TryGetValue(t, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    nsIn[t] = set;
                }
                set.Add(source);
            }
        }

        return nsIn;
    }

    private static IReadOnlyList<NamespaceCouplingMetrics> ComputeNamespaceMetrics(
        List<NamespaceNode> namespaces,
        IReadOnlyDictionary<string, HashSet<string>> nsIn,
        IReadOnlyDictionary<string, HashSet<string>> nsOut)
    {
        var result = new List<NamespaceCouplingMetrics>(namespaces.Count);

        foreach (var ns in namespaces)
        {
            var name = ns.Name;

            var ca = nsIn.TryGetValue(name, out var inSet) ? inSet.Count : 0;
            var ce = nsOut.TryGetValue(name, out var outSet) ? outSet.Count : 0;

            var instability = ComputeInstability(ca, ce);
            var abstractness = ComputeAbstractness(ns);
            var distance = Math.Abs(abstractness + instability - 1.0);

            result.Add(new NamespaceCouplingMetrics(
                Namespace: name,
                Ca: ca,
                Ce: ce,
                Instability: instability,
                Abstractness: abstractness,
                NormalizedDistance: distance
            ));
        }

        return result;
    }

    private static double ComputeInstability(int ca, int ce)
    {
        var denom = ca + ce;
        return denom > 0 ? ce / (double)denom : 0.0;
    }

    private static double ComputeAbstractness(NamespaceNode ns)
    {
        var total = ns.Types.Count;
        if (total == 0) return 0.0;

        var abs = ns.Types.Count(t => t.IsAbstract || t.Kind == TypeKind.Interface);
        return abs / (double)total;
    }

    #endregion

    #region Types

    private static (List<TypeNode> allTypes, HashSet<string> typeNames) CollectTypes(CodeModel model)
    {
        var allTypes = model.Codebase.Modules
            .SelectMany(m => m.Namespaces)
            .SelectMany(n => n.Types)
            .ToList();

        var typeNames = new HashSet<string>(allTypes.Select(t => t.FullName), StringComparer.Ordinal);
        return (allTypes, typeNames);
    }

    private static Dictionary<string, HashSet<string>> BuildTypeOutInternal(
        CodeModel model,
        HashSet<string> typeNames)
    {
        var outMap = typeNames.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var kv in model.DependencyGraph.TypeEdges)
        {
            if (!outMap.TryGetValue(kv.Key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                outMap[kv.Key] = set;
            }

            foreach (var target in kv.Value ?? Array.Empty<string>())
            {
                if (typeNames.Contains(target) && !string.Equals(kv.Key, target, StringComparison.Ordinal))
                    set.Add(target);
            }
        }

        return outMap;
    }

    private static Dictionary<string, HashSet<string>> BuildTypeInInternal(
        Dictionary<string, HashSet<string>> typeOutInternal,
        HashSet<string> typeNames)
    {
        var inMap = typeNames.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var (source, targets) in typeOutInternal)
        {
            foreach (var t in targets)
            {
                if (!inMap.TryGetValue(t, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    inMap[t] = set;
                }
                set.Add(source);
            }
        }

        return inMap;
    }

    private static IReadOnlyList<TypeCouplingMetrics> ComputeTypeMetrics(
        List<TypeNode> allTypes,
        IReadOnlyDictionary<string, HashSet<string>> typeOutInternal,
        IReadOnlyDictionary<string, HashSet<string>> typeInInternal)
    {
        var list = new List<TypeCouplingMetrics>(allTypes.Count);

        foreach (var t in allTypes)
        {
            var dep = DistinctDependencyCount(t);

            var iDep = typeOutInternal.TryGetValue(t.FullName, out var outs) ? outs.Count : 0;
            var fanOut = iDep;

            var fanIn = typeInInternal.TryGetValue(t.FullName, out var ins) ? ins.Count : 0;

            list.Add(new TypeCouplingMetrics(
                TypeFullName: t.FullName,
                Dependencies: dep,
                InternalDependencies: iDep,
                FanIn: fanIn,
                FanOut: fanOut
            ));
        }

        return list;
    }

    private static int DistinctDependencyCount(TypeNode t)
    {
        if (t.DependsOnTypes is null || t.DependsOnTypes.Count == 0)
            return 0;

        return new HashSet<string>(t.DependsOnTypes, StringComparer.Ordinal).Count;
    }

    #endregion
}
