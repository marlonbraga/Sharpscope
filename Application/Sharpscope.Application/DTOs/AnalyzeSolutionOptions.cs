using System.Collections.Generic;

namespace Sharpscope.Application.DTOs;

/// <summary>
/// Options that control report creation and output.
/// </summary>
public sealed class AnalyzeSolutionOptions
{
    /// <summary>Report formats to generate. Default: ["json"].</summary>
    public IReadOnlyList<string> Formats { get; init; } = new[] { "json" };

    /// <summary>Convenience field. If provided, overrides Formats to a single entry.</summary>
    public string? Format
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                Formats = new[] { value!.Trim().ToLowerInvariant() };
        }
    }

    /// <summary>Optional directory where reports will be written. Defaults to a temp directory.</summary>
    public string? OutputDirectory { get; init; }

    /// <summary>Optional base file name without extension. Default: timestamped name decided by the server.</summary>
    public string? OutputFileName { get; init; }

    /// <summary>
    /// Preferred response disposition: "inline" or "attachment".
    /// If both this and query ?disposition are provided, query wins.
    /// </summary>
    public string? Disposition { get; init; }

    /// <summary>
    /// Convenience flag: true => "attachment", false => "inline".
    /// Ignored if Disposition or query ?disposition are present.
    /// </summary>
    public bool? Download { get; init; }
}
