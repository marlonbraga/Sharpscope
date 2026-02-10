using System;
using System.IO;
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

    public async Task WriteAsync(AnalysisSnapshot snapshot, FileInfo outputFile, CancellationToken ct)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (outputFile is null) throw new ArgumentNullException(nameof(outputFile));

        outputFile.Directory?.Create();

        var sb = new StringBuilder();
        sb.AppendLine("Name,Count");
        sb.AppendLine($"Namespaces,{snapshot.Metrics.Namespaces.Count}");
        sb.AppendLine($"Types,{snapshot.Metrics.Types.Count}");
        sb.AppendLine($"Methods,{snapshot.Metrics.Methods.Count}");
        sb.AppendLine($"Projects,{snapshot.Metrics.Projects.Count}");
        sb.AppendLine($"NamespaceCoupling,{snapshot.Metrics.NamespaceCoupling.Count}");
        sb.AppendLine($"TypeCoupling,{snapshot.Metrics.TypeCoupling.Count}");

        await File.WriteAllTextAsync(outputFile.FullName, sb.ToString(), ct).ConfigureAwait(false);
    }

    #endregion
}
