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
        var nsList = model.Codebase.Modules
            .SelectMany(m => m.Namespaces)
            .ToList();

        var allNsNames = new HashSet<string>(nsList.Select(n => n.Name));

        // Outgoing edges per namespace (default to empty set)
        var nsOut = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var nsName in allNsNames)
        {
            nsOut[nsName] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var kv in model.DependencyGraph.NamespaceEdges)
        {
            if (!nsOut.ContainsKey(kv.Key))
                nsOut[kv.Key] = new HashSet<string>(StringComparer.Ordinal);

            foreach (var target in kv.Value ?? Array.Empty<string>())
            {
                // Avoid self-loops when counting CE; only cross-namespace deps matter for coupling
                if (!string.Equals(kv.Key, target, StringComparison.Ordinal))
                    nsOut[kv.Key].Add(target);
            }
        }

        // Build incoming edges map (for CA)
        var nsIn = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var nsName in allNsNames)
        {
            nsIn[nsName] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var (source, targets) in nsOut)
        {
            foreach (var t in targets)
            {
                if (!nsIn.ContainsKey(t))
                    nsIn[t] = new HashSet<string>(StringComparer.Ordinal);
                nsIn[t].Add(source);
            }
        }

        // Helper to count abstract types within a namespace (interfaces count as abstract)
        static (int totalTypes, int abstractTypes) CountAbstractness(NamespaceNode ns)
        {
            var total = ns.Types.Count;
            var abs = ns.Types.Count(t => t.IsAbstract || t.Kind == TypeKind.Interface);
            return (total, abs);
        }

        var result = new List<NamespaceCouplingMetrics>(nsList.Count);
        foreach (var ns in nsList)
        {
            var name = ns.Name;

            var ca = nsIn.TryGetValue(name, out var inSet) ? inSet.Count : 0;
            var ce = nsOut.TryGetValue(name, out var outSet) ? outSet.Count : 0;

            var denom = ca + ce;
            var instability = denom > 0 ? ce / (double)denom : 0.0;

            var (totalTypes, abstractTypes) = CountAbstractness(ns);
            var abstractness = totalTypes > 0 ? abstractTypes / (double)totalTypes : 0.0;

            var d = Math.Abs(abstractness + instability - 1.0);

            result.Add(new NamespaceCouplingMetrics(
                Namespace: name,
                Ca: ca,
                Ce: ce,
                Instability: instability,
                Abstractness: abstractness,
                NormalizedDistance: d
            ));
        }

        return result;
    }

    /// <summary>
    /// Computes coupling metrics for each type (DEP, I-DEP, FAN-IN, FAN-OUT).
    /// </summary>
    public IReadOnlyList<TypeCouplingMetrics> ComputeTypeCoupling(CodeModel model)
    {
        // Collect all model types (internal universe)
        var allTypes = model.Codebase.Modules
            .SelectMany(m => m.Namespaces)
            .SelectMany(n => n.Types)
            .ToList();

        var typeNames = new HashSet<string>(allTypes.Select(t => t.FullName), StringComparer.Ordinal);

        // Build a normalized internal dependency graph (distinct targets only)
        var typeOutInternal = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var t in typeNames)
            typeOutInternal[t] = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kv in model.DependencyGraph.TypeEdges)
        {
            if (!typeOutInternal.ContainsKey(kv.Key))
                typeOutInternal[kv.Key] = new HashSet<string>(StringComparer.Ordinal);

            foreach (var target in kv.Value ?? Array.Empty<string>())
            {
                if (typeNames.Contains(target) && !string.Equals(kv.Key, target, StringComparison.Ordinal))
                    typeOutInternal[kv.Key].Add(target);
            }
        }

        // Build incoming map for FAN-IN
        var typeInInternal = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var t in typeNames)
            typeInInternal[t] = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (source, targets) in typeOutInternal)
        {
            foreach (var t in targets)
            {
                if (!typeInInternal.ContainsKey(t))
                    typeInInternal[t] = new HashSet<string>(StringComparer.Ordinal);
                typeInInternal[t].Add(source);
            }
        }

        // Compute metrics per type
        var list = new List<TypeCouplingMetrics>(allTypes.Count);
        foreach (var t in allTypes)
        {
            // DEP: distinct dependencies declared by the type (including external)
            var depDistinct = new HashSet<string>(t.DependsOnTypes ?? Array.Empty<string>(), StringComparer.Ordinal);
            var dep = depDistinct.Count;

            // I-DEP and FAN-OUT: internal out-degree
            var iDep = typeOutInternal.TryGetValue(t.FullName, out var outs) ? outs.Count : 0;
            var fanOut = iDep;

            // FAN-IN: number of other types that depend on this type (internal)
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
}
