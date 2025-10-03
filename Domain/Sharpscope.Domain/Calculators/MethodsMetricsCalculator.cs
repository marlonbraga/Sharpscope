using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Calculators;

/// <summary>
/// Computes per-method metrics (MLOC, CYCLO, CALLS, NBD, PARAM) from the language-agnostic IR.
/// </summary>
public sealed class MethodsMetricsCalculator
{
    /// <summary>
    /// Computes metrics for a single method node.
    /// </summary>
    public MethodMetrics Compute(MethodNode method)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));

        var mloc = ClampNonNegative(method.Sloc);
        var cyclo = ComputeCyclomatic(method.DecisionPoints);
        var calls = ClampNonNegative(method.Calls);
        var nbd = ClampNonNegative(method.MaxNestingDepth);
        var @params = ClampNonNegative(method.Parameters);

        return new MethodMetrics(
            MethodFullName: method.FullName,
            Mloc: mloc,
            Cyclo: cyclo,
            Calls: calls,
            Nbd: nbd,
            Parameters: @params
        );
    }

    /// <summary>
    /// Computes metrics for all methods found in the <see cref="CodeModel"/>.
    /// </summary>
    public IReadOnlyList<MethodMetrics> ComputeAll(CodeModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        return CollectMethods(model)
            .Select(Compute)
            .ToList();
    }

    #region helpers

    private static IEnumerable<MethodNode> CollectMethods(CodeModel model) =>
        model.Codebase.Modules
            .SelectMany(m => m.Namespaces)
            .SelectMany(n => n.Types)
            .SelectMany(t => t.Methods);

    private static int ComputeCyclomatic(int decisionPoints)
    {
        // Cyclomatic = 1 + decision points, minimum 1
        var dp = ClampNonNegative(decisionPoints);
        return 1 + dp;
    }

    private static int ClampNonNegative(int value) =>
        value < 0 ? 0 : value;

    #endregion
}
