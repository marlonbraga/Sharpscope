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
/// Writes a simple CSV with high-level collection counts: Name,Count
/// (e.g., Namespaces, Types, Methods, TypeCoupling, NamespaceCoupling).
/// </summary>
public sealed class CsvReportWriter : IReportWriter
{
    public string Format => "csv";

    #region Public API

    public async Task WriteAsync(MetricsResult result, FileInfo outputFile, CancellationToken ct)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        if (outputFile is null) throw new ArgumentNullException(nameof(outputFile));

        outputFile.Directory?.Create();

        var sb = new StringBuilder();
        sb.AppendLine("Name,Count");

        foreach (var p in result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var val = p.GetValue(result);
            if (val is null) continue;
            if (val is IEnumerable en && val is not string)
            {
                var count = 0;
                foreach (var _ in en) count++;
                sb.AppendLine($"{p.Name},{count}");
            }
        }

        await File.WriteAllTextAsync(outputFile.FullName, sb.ToString(), ct).ConfigureAwait(false);
    }

    #endregion
}
