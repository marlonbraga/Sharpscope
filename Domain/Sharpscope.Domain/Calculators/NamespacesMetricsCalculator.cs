using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

/// <summary>
/// Computes namespace-level metrics:
/// - NOC: number of types in the namespace
/// - NAC: number of abstract classes/records in the namespace (interfaces are not counted)
/// </summary>
public sealed class NamespacesMetricsCalculator
{
    #region Public API

    /// <summary>
    /// Computes <see cref="NamespaceMetrics"/> for every namespace in the <paramref name="model"/>.
    /// </summary>
    public IReadOnlyList<NamespaceMetrics> ComputeAll(CodeModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        return CollectNamespaces(model)
            .Select(ComputeFor)
            .ToList();
    }

    /// <summary>
    /// Computes <see cref="NamespaceMetrics"/> for a single <see cref="NamespaceNode"/>.
    /// </summary>
    public NamespaceMetrics ComputeFor(NamespaceNode ns)
    {
        if (ns is null) throw new ArgumentNullException(nameof(ns));

        var noc = ns.Types.Count;
        var nac = CountAbstractClassLike(ns);

        return new NamespaceMetrics(
            Namespace: ns.Name,
            Noc: noc,
            Nac: nac
        );
    }

    #endregion

    #region Helpers

    private static IEnumerable<NamespaceNode> CollectNamespaces(CodeModel model) =>
        model.Codebase.Modules.SelectMany(m => m.Namespaces);

    private static int CountAbstractClassLike(NamespaceNode ns)
    {
        // Interfaces are not counted towards NAC (it stands for "abstract classes")
        // Records can be abstract; structs/enums cannot.
        return ns.Types.Count(t =>
            t.IsAbstract &&
            (t.Kind == TypeKind.Class || t.Kind == TypeKind.Record));
    }

    #endregion
}
