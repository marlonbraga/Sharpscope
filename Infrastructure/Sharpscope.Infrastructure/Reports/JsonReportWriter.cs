using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Reports;

/// <summary>
/// Writes the entire AnalysisSnapshot as indented JSON.
/// </summary>
public sealed class JsonReportWriter : IReportWriter
{
    public string Format => "json";

    #region Public API

    public async Task WriteAsync(AnalysisSnapshot snapshot, FileInfo outputFile, CancellationToken ct)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (outputFile is null) throw new ArgumentNullException(nameof(outputFile));

        outputFile.Directory?.Create();

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        await using var fs = outputFile.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, snapshot, opts, ct).ConfigureAwait(false);
    }

    #endregion
}
