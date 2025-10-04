// Domain.Contracts
using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Contracts;

public interface IReportWriter
{
    string Format { get; } // "json" | "md" | "csv" | "sarif"
    Task WriteAsync(MetricsResult result, FileInfo output, CancellationToken ct);
}
