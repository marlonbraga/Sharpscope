using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Reports;

/// <summary>
/// Emits a minimal SARIF 2.1.0 artifact with Sharpscope metadata and collection counts in run.properties.
/// </summary>
public sealed class SarifReportWriter : IReportWriter
{
    public string Format => "sarif";

    public async Task WriteAsync(AnalysisSnapshot snapshot, FileInfo outputFile, CancellationToken ct)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (outputFile is null) throw new ArgumentNullException(nameof(outputFile));

        outputFile.Directory?.Create();

        var props = CollectCounts(snapshot);

        // Use dictionaries to allow the "$schema" property name
        var root = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$schema"] = "https://schemastore.azurewebsites.net/schemas/json/sarif-2.1.0-rtm.5.json",
            ["version"] = "2.1.0",
            ["runs"] = new object[]
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["tool"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["driver"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["name"] = "Sharpscope",
                            ["informationUri"] = "https://github.com/marlonbraga/sharpscope",
                            ["version"] = snapshot.Metadata.ToolVersion
                        }
                    },
                    ["properties"] = props,
                    ["invocations"] = new object[]
                    {
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["executionSuccessful"] = true,
                            ["startTimeUtc"] = DateTimeOffset.UtcNow
                        }
                    },
                    ["results"] = Array.Empty<object>()
                }
            }
        };

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        await using var fs = outputFile.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, root, opts, ct).ConfigureAwait(false);
    }

    private static Dictionary<string, int> CollectCounts(AnalysisSnapshot snapshot)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Namespaces"] = snapshot.Metrics.Namespaces.Count,
            ["Types"] = snapshot.Metrics.Types.Count,
            ["Methods"] = snapshot.Metrics.Methods.Count,
            ["Projects"] = snapshot.Metrics.Projects.Count,
            ["NamespaceCoupling"] = snapshot.Metrics.NamespaceCoupling.Count,
            ["TypeCoupling"] = snapshot.Metrics.TypeCoupling.Count
        };
        return dict;
    }
}
