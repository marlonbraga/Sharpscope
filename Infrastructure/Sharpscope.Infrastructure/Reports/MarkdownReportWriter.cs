using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Reports;

/// <summary>
/// Writes a human-friendly Markdown summary. 
/// It gracefully handles nulls and unknown shapes by reflecting basic counts from collections.
/// </summary>
public sealed class MarkdownReportWriter : IReportWriter
{
    public string Format => "md";

    #region Public API

    public async Task WriteAsync(MetricsResult result, FileInfo outputFile, CancellationToken ct)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        if (outputFile is null) throw new ArgumentNullException(nameof(outputFile));

        outputFile.Directory?.Create();

        var sb = new StringBuilder();
        sb.AppendLine("# Sharpscope Report");
        sb.AppendLine();
        sb.AppendLine($"> Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();

        // Summary (se existir)
        var summary = GetPropValue(result, "Summary");
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

        // Counts de coleções de alto nível (Namespaces, Types, Methods, etc.)
        sb.AppendLine("## Counts");
        var topProps = result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var p in topProps)
        {
            var val = p.GetValue(result);
            if (val is null) continue;

            if (val is IEnumerable en && val is not string)
            {
                var count = 0;
                foreach (var _ in en) count++;
                sb.AppendLine($"- **{p.Name}**: {count}");
            }
        }

        await File.WriteAllTextAsync(outputFile.FullName, sb.ToString(), ct).ConfigureAwait(false);
    }

    #endregion

    #region Helpers

    private static object? GetPropValue(object obj, string name) =>
        obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(obj);

    #endregion
}
