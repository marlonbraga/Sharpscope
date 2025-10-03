using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

/// <summary>
/// Computes per-type metrics:
/// SLOC, NOM, NPM, WMC, DEP, I-DEP, FAN-IN, FAN-OUT, NOA, LCOM3.
/// </summary>
public sealed class TypesMetricsCalculator
{
    #region Public API

    /// <summary>
    /// Computes <see cref="TypeMetrics"/> for a single type using the surrounding <see cref="CodeModel"/>
    /// to resolve coupling metrics (I-DEP, FAN-IN, FAN-OUT).
    /// </summary>
    public TypeMetrics ComputeFor(TypeNode type, CodeModel model)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (model is null) throw new ArgumentNullException(nameof(model));

        var (typeNames, outMap, inMap) = BuildTypeGraphs(model);

        var sloc = SumNonNegative(type.Methods.Select(m => m.Sloc));
        var nom = type.Methods.Count;
        var npm = type.Methods.Count(m => m.IsPublic);
        var wmc = type.Methods.Sum(m => 1 + ClampNonNegative(m.DecisionPoints));
        var dep = DistinctCount(type.DependsOnTypes);

        var iDep = CountOutInternal(type.FullName, outMap);
        var fanOut = iDep;
        var fanIn = CountInInternal(type.FullName, inMap);

        var noa = type.Fields.Count;
        var lcom3 = ComputeLcom3(type);

        return new TypeMetrics(
            TypeFullName: type.FullName,
            Sloc: sloc,
            Nom: nom,
            Npm: npm,
            Wmc: wmc,
            Dep: dep,
            IDep: fanOut,
            FanIn: fanIn,
            FanOut: fanOut,
            Noa: noa,
            Lcom3: lcom3
        );
    }

    /// <summary>
    /// Computes <see cref="TypeMetrics"/> for every type present in <paramref name="model"/>.
    /// </summary>
    public IReadOnlyList<TypeMetrics> ComputeAll(CodeModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        var allTypes = CollectTypes(model).ToList();
        var graphs = BuildTypeGraphs(model);
        var list = new List<TypeMetrics>(allTypes.Count);

        foreach (var t in allTypes)
            list.Add(ComputeForWithGraphs(t, graphs));
        
        return list;
    }


    #endregion

    #region Calculation (single type with prebuilt graphs)

    private TypeMetrics ComputeForWithGraphs(
        TypeNode type,
        (HashSet<string> typeNames,
         IReadOnlyDictionary<string, HashSet<string>> outMap,
         IReadOnlyDictionary<string, HashSet<string>> inMap) graphs)
    {
        var (_, outMap, inMap) = graphs;

        var sloc = SumNonNegative(type.Methods.Select(m => m.Sloc));
        var nom = type.Methods.Count;
        var npm = type.Methods.Count(m => m.IsPublic);
        var wmc = type.Methods.Sum(m => 1 + ClampNonNegative(m.DecisionPoints));
        var dep = DistinctCount(type.DependsOnTypes);

        var iDep = CountOutInternal(type.FullName, outMap);
        var fanOut = iDep;
        var fanIn = CountInInternal(type.FullName, inMap);

        var noa = type.Fields.Count;
        var lcom3 = ComputeLcom3(type);

        return new TypeMetrics(
            TypeFullName: type.FullName,
            Sloc: sloc,
            Nom: nom,
            Npm: npm,
            Wmc: wmc,
            Dep: dep,
            IDep: fanOut,
            FanIn: fanIn,
            FanOut: fanOut,
            Noa: noa,
            Lcom3: lcom3
        );
    }

    #endregion

    #region Helpers (graphs, math and collectors)

    private static (HashSet<string> typeNames,
                    IReadOnlyDictionary<string, HashSet<string>> outMap,
                    IReadOnlyDictionary<string, HashSet<string>> inMap)
        BuildTypeGraphs(CodeModel model)
    {
        var typeNames = CollectTypes(model)
            .Select(t => t.FullName)
            .ToHashSet(StringComparer.Ordinal);

        var outMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var name in typeNames)
            outMap[name] = new HashSet<string>(StringComparer.Ordinal);

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

        var inMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var name in typeNames)
            inMap[name] = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (source, targets) in outMap)
        {
            foreach (var t in targets)
            {
                if (!inMap.TryGetValue(t, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    inMap[t] = set;
                }
                inMap[t].Add(source);
            }
        }

        return (typeNames, outMap, inMap);
    }

    private static IEnumerable<TypeNode> CollectTypes(CodeModel model) =>
        model.Codebase.Modules
            .SelectMany(m => m.Namespaces)
            .SelectMany(n => n.Types);

    private static int DistinctCount(IReadOnlyList<string>? items)
        => items is null || items.Count == 0
            ? 0
            : new HashSet<string>(items, StringComparer.Ordinal).Count;

    private static int CountOutInternal(string typeFullName, IReadOnlyDictionary<string, HashSet<string>> outMap)
        => outMap.TryGetValue(typeFullName, out var set) ? set.Count : 0;

    private static int CountInInternal(string typeFullName, IReadOnlyDictionary<string, HashSet<string>> inMap)
        => inMap.TryGetValue(typeFullName, out var set) ? set.Count : 0;

    private static int SumNonNegative(IEnumerable<int> values)
        => values.Select(ClampNonNegative).Sum();

    private static int ClampNonNegative(int v) => v < 0 ? 0 : v;

    /// <summary>
    /// LCOM3 (bounded version): 1 - (sum μ(a) / m) / n = 1 - sumμ / (m*n).
    /// If m == 0 or n == 0, returns 0.0.
    /// </summary>
    private static double ComputeLcom3(TypeNode type)
    {
        var m = type.Methods.Count;
        var n = type.Fields.Count;
        if (m <= 1 || n <= 1) return 0.0;

        // μ(a): number of methods that access field a
        var fields = type.Fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);

        var muSum = 0;
        foreach (var field in fields)
        {
            var mu = type.Methods.Count(md => md.AccessedFields?.Contains(field) == true);
            muSum += mu;
        }

        var denom = m * n;
        if (denom == 0) return 0.0;

        var raw = 1.0 - (muSum / (double)denom);
        if (raw < 0) return 0.0;
        if (raw > 1) return 1.0;
        return raw;
    }

    #endregion
}
