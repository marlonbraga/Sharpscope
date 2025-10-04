using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Adapters.CSharp.Roslyn.Modeling;

/// <summary>
/// Builds namespace/type dependency graphs from collected <see cref="TypeNode"/> dependencies.
/// </summary>
public static class DependencyGraphBuilder
{
    #region Public API

    public static DependencyGraph Build(IReadOnlyCollection<TypeNode> types)
    {
        if (types is null) throw new ArgumentNullException(nameof(types));

        var typeMap = types.ToDictionary(t => t.FullName, StringComparer.Ordinal);
        var typeEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var t in types)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dep in t.DependsOnTypes ?? Array.Empty<string>())
            {
                if (string.Equals(dep, t.FullName, StringComparison.Ordinal)) continue;     // ignore self
                if (typeMap.ContainsKey(dep)) set.Add(dep);                                // internal only
            }
            typeEdges[t.FullName] = set;
        }

        var nsEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var kv in typeEdges)
        {
            var srcNs = NamespaceOf(kv.Key);
            foreach (var tgt in kv.Value)
            {
                var dstNs = NamespaceOf(tgt);
                if (string.Equals(srcNs, dstNs, StringComparison.Ordinal)) continue;
                nsEdges.TryAdd(srcNs, new HashSet<string>(StringComparer.Ordinal));
                nsEdges[srcNs].Add(dstNs);
            }
        }

        return new DependencyGraph(
            typeEdges.ToDictionary(k => k.Key, v => (IReadOnlyCollection<string>)v.Value, StringComparer.Ordinal),
            nsEdges.ToDictionary(k => k.Key, v => (IReadOnlyCollection<string>)v.Value, StringComparer.Ordinal));
    }

    #endregion

    #region Helpers

    private static string NamespaceOf(string fullTypeName)
    {
        var idx = fullTypeName.LastIndexOf('.');
        return idx <= 0 ? "" : fullTypeName.Substring(0, idx);
    }

    #endregion
}
