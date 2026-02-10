using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Contracts;

/// <summary>
/// Computes all metrics from the language-agnostic IR.
/// </summary>
public interface IMetricsEngine
{
    /// <summary>
    /// Computes the full set of metrics (the 44 requested ones) from <see cref="CodeGraph"/>.
    /// </summary>
    MetricsSnapshot Compute(CodeGraph graph);
}
