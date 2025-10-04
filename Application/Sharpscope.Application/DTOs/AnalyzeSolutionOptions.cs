using System.Collections.Generic;

namespace Sharpscope.Application.DTOs;

/// <summary>
/// Options that control report creation and output.
/// </summary>
public sealed class AnalyzeSolutionOptions
{
    /// <summary>
    /// Report formats to generate, e.g., ["json","md","csv"]. Default: ["json"].
    /// </summary>
    public IReadOnlyList<string> Formats { get; init; } = new[] { "json" };

    /// <summary>
    /// Optional directory where reports will be written. Defaults to a temp directory.
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// Optional base file name without extension. Default: "sharpscope-report".
    /// </summary>
    public string? OutputFileName { get; init; }
}