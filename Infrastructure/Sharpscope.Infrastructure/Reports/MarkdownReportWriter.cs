using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Reports;

/// <summary>
/// Writes a human-friendly Markdown summary.
/// </summary>
public sealed class MarkdownReportWriter : IReportWriter
{
    public string Format => "md";

    #region Public API

    public async Task WriteAsync(AnalysisSnapshot snapshot, FileInfo outputFile, CancellationToken ct)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (outputFile is null) throw new ArgumentNullException(nameof(outputFile));

        outputFile.Directory?.Create();

        var sb = new StringBuilder();
        sb.AppendLine("# Sharpscope Report");
        sb.AppendLine();
        sb.AppendLine($"> Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();

        var summary = snapshot.Metrics.Summary;
        if (summary is not null)
        {
            sb.AppendLine("## Summary");
            foreach (var p in summary.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var v = p.GetValue(summary);
                if (v is null) continue;
                sb.AppendLine($"- **{p.Name}**: {v}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Counts");
        sb.AppendLine($"- **Namespaces**: {snapshot.Metrics.Namespaces.Count}");
        sb.AppendLine($"- **Types**: {snapshot.Metrics.Types.Count}");
        sb.AppendLine($"- **Methods**: {snapshot.Metrics.Methods.Count}");
        sb.AppendLine($"- **Projects**: {snapshot.Metrics.Projects.Count}");
        sb.AppendLine($"- **NamespaceCoupling**: {snapshot.Metrics.NamespaceCoupling.Count}");
        sb.AppendLine($"- **TypeCoupling**: {snapshot.Metrics.TypeCoupling.Count}");

        await File.WriteAllTextAsync(outputFile.FullName, sb.ToString(), ct).ConfigureAwait(false);
    }

    #endregion
}
