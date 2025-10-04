using System;
using System.Collections;
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

    public async Task WriteAsync(MetricsResult result, FileInfo outputFile, CancellationToken ct)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        if (outputFile is null) throw new ArgumentNullException(nameof(outputFile));

        outputFile.Directory?.Create();

        var props = CollectCounts(result);

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
                            ["version"] = "0.1.0"
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

    private static Dictionary<string, int> CollectCounts(MetricsResult result)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in result.GetType().GetProperties())
        {
            var val = p.GetValue(result);
            if (val is null) continue;
            if (val is IEnumerable en && val is not string)
            {
                var count = 0;
                foreach (var _ in en) count++;
                dict[p.Name] = count;
            }
        }
        return dict;
    }
}
