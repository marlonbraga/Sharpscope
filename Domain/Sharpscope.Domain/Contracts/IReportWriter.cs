using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Contracts;

/// <summary>
/// Writes the computed metrics to a chosen output format (e.g., JSON, CSV, Markdown, SARIF).
/// </summary>
public interface IReportWriter
{
    /// <summary>
    /// Writes the <paramref name="result"/> to <paramref name="output"/> using the provided <paramref name="format"/>.
    /// </summary>
    Task WriteAsync(MetricsResult result, FileInfo output, string format, CancellationToken ct);
}
